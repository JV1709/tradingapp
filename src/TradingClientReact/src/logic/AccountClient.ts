import { Account, CreateAccountRequest } from './types';

export interface AccountClientConfig {
  hostname: string;
}

export class AccountClient {
  private readonly config: AccountClientConfig;
  private readonly baseUri = 'accounts';
  private abortControllers: Map<string, AbortController> = new Map();

  constructor(config: AccountClientConfig) {
    this.config = config;
  }

  /**
   * Creates a new account via REST POST.
   */
  async createAccount(username: string, initialBalance: number): Promise<Account> {
    const request: CreateAccountRequest = {
      Username: username,
      InitialBalance: initialBalance,
    };

    try {
      const response = await fetch(`${this.config.hostname}/${this.baseUri}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to create account: ${errorText || response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      console.error(`Error creating account for ${username}:`, error);
      throw error;
    }
  }

  /**
   * Subscribes to account updates via HTTP Streaming.
   */
  async subscribe(username: string, onUpdate: (account: Account) => void): Promise<void> {
    if (this.abortControllers.has(username)) {
      return; // Already subscribed
    }

    const abortController = new AbortController();
    this.abortControllers.set(username, abortController);

    try {
      const response = await fetch(`${this.config.hostname}/${this.baseUri}/stream/${username}`, {
        signal: abortController.signal,
      });

      if (!response.ok) {
        throw new Error(`Failed to subscribe to account ${username}: ${response.statusText}`);
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
            const account: Account = JSON.parse(payload);
            onUpdate(account);
          } catch (err) {
            console.error('Error parsing account SSE JSON:', err, 'Payload:', payload);
          }
        }
      }
    } catch (error: any) {
      if (error.name === 'AbortError') {
        console.log(`Subscription for ${username} was cancelled.`);
      } else {
        console.error(`Error in account stream for ${username}:`, error);
        throw error;
      }
    } finally {
      this.abortControllers.delete(username);
    }
  }

  /**
   * Unsubscribes from account updates.
   */
  unsubscribe(username: string): void {
    const controller = this.abortControllers.get(username);
    if (controller) {
      controller.abort();
      this.abortControllers.delete(username);
    }
  }

  /**
   * Closes all active subscriptions.
   */
  dispose(): void {
    for (const username of this.abortControllers.keys()) {
      this.unsubscribe(username);
    }
  }
}
