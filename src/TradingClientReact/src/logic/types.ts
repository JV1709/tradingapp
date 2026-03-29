/**
 * Represents the order status, matching the C# OrderStatus enum.
 */
export enum OrderStatus {
  New = 0,
  PartiallyFilled = 1,
  Filled = 2,
  Cancelled = 4,
  PendingCancel = 6,
  Rejected = 8,
  PendingNew = 'A',
}

/**
 * Represents the order side (Buy/Sell).
 */
export enum Side {
  Buy = 1,
  Sell = 2,
}

/**
 * Represents an Order in the system.
 */
export interface Order {
  OrderId: string; // Guid in C#
  AccountKey: string;
  Status: OrderStatus;
  Symbol: string;
  TotalQuantity: number;
  FilledQuantity: number;
  Price: number;
  Side: Side;
}

/**
 * Represents a holding in an account.
 */
export interface Holding {
  Symbol: string;
  Quantity: number;
}

/**
 * Represents a user account.
 */
export interface Account {
  Username: string;
  TotalBalance: number;
  AvailableBalance: number;
  Holdings: Holding[];
}

/**
 * Request to create a new account.
 */
export interface CreateAccountRequest {
  Username: string;
  InitialBalance: number;
}

/**
 * Market data quote.
 */
export interface Quote {
  Symbol: string;
  BidPrice?: number;
  AskPrice?: number;
  LastDonePrice?: number;
  Timestamp: string; // DateTime in C#
}

/**
 * Tradable instrument.
 */
export interface Instrument {
  Symbol: string;
  Name: string;
}

/**
 * Base for events.
 */
export interface EventBase {
  // Empty currently in C# but reserved for future use
}

/**
 * Event published when an order is updated.
 */
export interface OrderUpdateEvent extends EventBase {
  Order: Order;
  Remark: string;
}

/**
 * Event published when an account is updated.
 */
export interface AccountUpdateEvent extends EventBase {
  Username: string;
  TotalBalance: number;
  AvailableBalance: number;
  Holdings: Holding[];
}

/**
 * Request types for the Gateway.
 */
export enum GatewayRequestType {
  PlaceOrder = 1,
  CancelOrder = 2,
}

/**
 * Request to place a new order.
 */
export interface PlaceOrderRequest {
  AccountKey: string;
  Symbol: string;
  Side: number; // 1 for Buy, 2 for Sell
  Quantity: number;
  Price: number;
}

/**
 * Request to cancel an existing order.
 */
export interface CancelOrderRequest {
  OrderId: string;
}

/**
 * Wrapper for requests sent to the OrderGateway.
 */
export interface GatewayRequest {
  type: GatewayRequestType;
  placeOrderRequest?: PlaceOrderRequest;
  cancelOrderRequest?: CancelOrderRequest;
}
