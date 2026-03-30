import { Quote } from './types';

export interface PriceClientConfig {
  hostname: string;
}

export class PriceClient {
  private readonly config: PriceClientConfig;
  private readonly baseUri = 'quotes';
  private abortControllers: Map<string, AbortController> = new Map();

  constructor(config: PriceClientConfig) {
    this.config = config;
  }

  /**
   * Subscribes to real-time quotes for a specific symbol via HTTP Streaming.
   */
  async subscribe(symbol: string, onUpdate: (quote: Quote) => void): Promise<void> {
    if (this.abortControllers.has(symbol)) {
      return; // Already subscribed
    }

    const abortController = new AbortController();
    this.abortControllers.set(symbol, abortController);

    try {
      const response = await fetch(`${this.config.hostname}/${this.baseUri}/stream/${symbol}`, {
        signal: abortController.signal,
      });

      if (!response.ok) {
        throw new Error(`Failed to subscribe to prices for ${symbol}: ${response.statusText}`);
      }

      const reader = response.body?.getReader();
      if (!reader) {
        throw new Error('ReadableStream not supported by browser or response body is empty.');
      }

      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const events = buffer.split(/\r?\n\r?\n/);
        buffer = events.pop() || ''; // Keep last partial SSE event in buffer

        for (const eventChunk of events) {
          const dataLines = eventChunk
            .split(/\r?\n/)
            .filter((line) => line.startsWith('data:'))
            .map((line) => line.slice(5).trim());

          if (dataLines.length === 0) {
            continue;
          }

          const payload = dataLines.join('\n');
          if (!payload) {
            continue;
          }

          try {
            const quote: Quote = JSON.parse(payload);
            onUpdate(quote);
          } catch (err) {
            console.error('Error parsing quote SSE JSON:', err, 'Payload:', payload);
          }
        }
      }
    } catch (error: any) {
      if (error.name === 'AbortError') {
        console.log(`Price subscription for ${symbol} was cancelled.`);
      } else {
        console.error(`Error in price stream for ${symbol}:`, error);
        throw error;
      }
    } finally {
      this.abortControllers.delete(symbol);
    }
  }

  /**
   * Unsubscribes from price updates for a specific symbol.
   */
  unsubscribe(symbol: string): void {
    const controller = this.abortControllers.get(symbol);
    if (controller) {
      controller.abort();
      this.abortControllers.delete(symbol);
    }
  }

  /**
   * Closes all active price subscriptions.
   */
  dispose(): void {
    for (const symbol of this.abortControllers.keys()) {
      this.unsubscribe(symbol);
    }
  }
}
