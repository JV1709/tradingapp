import { AccountClient, AccountClientConfig } from './AccountClient';
import { InstrumentClient, InstrumentClientConfig } from './InstrumentClient';
import { OrderClient, OrderClientConfig } from './OrderClient';
import { PriceClient, PriceClientConfig } from './PriceClient';

export interface TradingClientConfig {
  accountClientConfig: AccountClientConfig;
  instrumentClientConfig: InstrumentClientConfig;
  orderClientConfig: OrderClientConfig;
  priceClientConfig: PriceClientConfig;
}

/**
 * Composite client that provides access to all trading services.
 */
export class TradingClient {
  public readonly accountClient: AccountClient;
  public readonly instrumentClient: InstrumentClient;
  public readonly orderClient: OrderClient;
  public readonly priceClient: PriceClient;

  constructor(config: TradingClientConfig) {
    this.accountClient = new AccountClient(config.accountClientConfig);
    this.instrumentClient = new InstrumentClient(config.instrumentClientConfig);
    this.orderClient = new OrderClient(config.orderClientConfig);
    this.priceClient = new PriceClient(config.priceClientConfig);
  }

  /**
   * Disposes all sub-clients and closes all active connections.
   */
  dispose(): void {
    this.accountClient.dispose();
    this.orderClient.dispose();
    this.priceClient.dispose();
  }
}
