import { Instrument } from './types';

const instrumentCacheByHost = new Map<string, Instrument[]>();
const instrumentRequestsByHost = new Map<string, Promise<Instrument[]>>();

export interface InstrumentClientConfig {
  hostname: string;
}

export class InstrumentClient {
  private readonly config: InstrumentClientConfig;
  private readonly baseUri = 'instruments';

  constructor(config: InstrumentClientConfig) {
    this.config = config;
  }

  /**
   * Fetches the list of all tradable instruments.
   */
  async getInstruments(): Promise<Instrument[]> {
    const hostKey = this.config.hostname || window.location.origin;
    const cached = instrumentCacheByHost.get(hostKey);
    if (cached) {
      return cached;
    }

    const existingRequest = instrumentRequestsByHost.get(hostKey);
    if (existingRequest) {
      return existingRequest;
    }

    const request = this.fetchInstruments();
    instrumentRequestsByHost.set(hostKey, request);

    try {
      const instruments = await request;
      instrumentCacheByHost.set(hostKey, instruments);
      return instruments;
    } catch (error) {
      console.error('Error fetching instruments:', error);
      throw error;
    } finally {
      instrumentRequestsByHost.delete(hostKey);
    }
  }

  clearCache(): void {
    const hostKey = this.config.hostname || window.location.origin;
    instrumentCacheByHost.delete(hostKey);
    instrumentRequestsByHost.delete(hostKey);
  }

  private async fetchInstruments(): Promise<Instrument[]> {
    const response = await fetch(`${this.config.hostname}/${this.baseUri}`);
    if (!response.ok) {
      throw new Error(`Failed to fetch instruments: ${response.statusText}`);
    }

    return await response.json();
  }
}
