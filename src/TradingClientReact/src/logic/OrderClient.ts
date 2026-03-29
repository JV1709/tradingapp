import {
  OrderUpdateEvent,
  PlaceOrderRequest,
  CancelOrderRequest
} from './types';

export interface OrderClientConfig {
  hostname: string;
  reconnectDelaySeconds?: number;
}

export class OrderClient {
  private readonly config: OrderClientConfig;
  private readonly baseUri = 'orders';
  private webSockets: Map<string, WebSocket> = new Map();
  private onUpdateCallback?: (event: OrderUpdateEvent) => void;

  constructor(config: OrderClientConfig) {
    this.config = config;
  }

  /**
   * Subscribes to order updates for a specific account via WebSocket.
   */
  async subscribe(username: string, onUpdate: (event: OrderUpdateEvent) => void): Promise<void> {
    if (this.webSockets.has(username)) {
      return; // Already subscribed
    }

    this.onUpdateCallback = onUpdate;

    const wsUrl = this.getWebSocketUri(username);
    const ws = new WebSocket(wsUrl);

    return new Promise((resolve, reject) => {
      ws.onopen = () => {
        this.webSockets.set(username, ws);
        console.log(`WebSocket connected for ${username}`);
        resolve();
      };

      ws.onmessage = (event) => {
        try {
          const update: OrderUpdateEvent = JSON.parse(event.data);
          if (this.onUpdateCallback) {
            this.onUpdateCallback(update);
          }
        } catch (err) {
          console.error(`Error parsing WebSocket message for ${username}:`, err);
        }
      };

      ws.onerror = (error) => {
        console.error(`WebSocket error for ${username}:`, error);
        reject(error);
      };

      ws.onclose = () => {
        console.log(`WebSocket closed for ${username}`);
        this.webSockets.delete(username);
        // We could implement auto-reconnect here if needed
      };
    });
  }

  private getWebSocketUri(username: string): string {
    const base = this.config.hostname || window.location.origin;
    const url = new URL(base.startsWith('http') ? base : `${window.location.protocol}//${window.location.host}${base}`);
    const protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    // When using proxy /order, the WS path should be relative to where the proxy is mounted
    // If hostname is empty, we are hitting the Vite server which proxies /order to the backend
    return `${protocol}//${url.host}/${this.baseUri}/ws/${username}`;
  }

  /**
   * Places a new order via WebSocket.
   */
  placeOrder(request: PlaceOrderRequest): void {
    const ws = this.webSockets.get(request.AccountKey);
    if (!ws || ws.readyState !== WebSocket.OPEN) {
      throw new Error(`Must subscribe to account '${request.AccountKey}' before placing orders.`);
    }

    // Send unwrapped request as expected by backend's manual JSON processing
    ws.send(JSON.stringify(request));
  }

  /**
   * Cancels an existing order via the first available WebSocket connection.
   */
  cancelOrder(request: CancelOrderRequest): void {
    if (this.webSockets.size === 0) {
      throw new Error('No active WebSocket connections found to cancel order.');
    }

    // Since CancelOrderRequest doesn't have an accountKey, we use the first open WS connection
    const ws = Array.from(this.webSockets.values()).find(w => w.readyState === WebSocket.OPEN);
    if (!ws) {
      throw new Error('No open WebSocket connection available to send cancel request.');
    }

    // Send unwrapped request as expected by backend's manual JSON processing
    ws.send(JSON.stringify(request));
  }

  /**
   * Unsubscribes from order updates and closes the WebSocket.
   */
  unsubscribe(username: string): void {
    const ws = this.webSockets.get(username);
    if (ws) {
      ws.close();
      this.webSockets.delete(username);
    }
  }

  /**
   * Closes all active WebSocket connections.
   */
  dispose(): void {
    for (const username of this.webSockets.keys()) {
      this.unsubscribe(username);
    }
  }
}
