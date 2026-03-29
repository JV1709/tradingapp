import { Instrument } from './types';

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
    try {
      const response = await fetch(`${this.config.hostname}/${this.baseUri}`);
      if (!response.ok) {
        throw new Error(`Failed to fetch instruments: ${response.statusText}`);
      }

      const instruments: Instrument[] = await response.json();
      return instruments;
    } catch (error) {
      console.error('Error fetching instruments:', error);
      throw error;
    }
  }
}
