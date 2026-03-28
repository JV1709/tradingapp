using Model.Domain;

namespace MatchingEngine
{
    public class OrderBook
    {
        public string Symbol { get; }
        
        // Asks are ordered ascending
        private readonly SortedDictionary<decimal, PriceLevel> _asks;
        // Bids are ordered descending
        private readonly SortedDictionary<decimal, PriceLevel> _bids;

        // Cancellation cache
        private readonly HashSet<Guid> _cancelledOrderIds;

        public OrderBook(string symbol)
        {
            Symbol = symbol;
            _asks = new SortedDictionary<decimal, PriceLevel>();
            _bids = new SortedDictionary<decimal, PriceLevel>(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
            _cancelledOrderIds = new HashSet<Guid>();
        }

        public void CancelOrder(Guid orderId)
        {
            _cancelledOrderIds.Add(orderId);
        }

        public void AddOrder(Order order)
        {
            // First check if the incoming order itself was cancelled before it even arrived at the book
            if (_cancelledOrderIds.Contains(order.OrderId))
            {
                _cancelledOrderIds.Remove(order.OrderId);
                return;
            }

            if (order.Side == Side.Buy)
            {
                MatchAndAdd(order, _asks, _bids);
            }
            else
            {
                MatchAndAdd(order, _bids, _asks);
            }
        }

        private void MatchAndAdd(Order takerOrder, SortedDictionary<decimal, PriceLevel> makerSide, SortedDictionary<decimal, PriceLevel> takerSide)
        {
            bool isBuy = takerOrder.Side == Side.Buy;

            while (takerOrder.FilledQuantity < takerOrder.TotalQuantity && makerSide.Count > 0)
            {
                var bestPriceLevelKv = makerSide.First();
                decimal bestPrice = bestPriceLevelKv.Key;
                PriceLevel bestLevel = bestPriceLevelKv.Value;

                bool priceMatches = takerOrder.Price == 0 || (isBuy ? takerOrder.Price >= bestPrice : takerOrder.Price <= bestPrice);
                
                if (!priceMatches)
                {
                    break;
                }

                while (takerOrder.FilledQuantity < takerOrder.TotalQuantity && bestLevel.Count > 0)
                {
                    var makerOrder = bestLevel.Peek();
                    
                    if (_cancelledOrderIds.Contains(makerOrder.OrderId))
                    {
                        bestLevel.Dequeue();
                        _cancelledOrderIds.Remove(makerOrder.OrderId);
                        continue;
                    }

                    long makerRemaining = makerOrder.TotalQuantity - makerOrder.FilledQuantity;
                    long takerRemaining = takerOrder.TotalQuantity - takerOrder.FilledQuantity;

                    long fillQuantity = Math.Min(makerRemaining, takerRemaining);

                    makerOrder.FilledQuantity += fillQuantity;
                    takerOrder.FilledQuantity += fillQuantity;

                    if (makerOrder.FilledQuantity == makerOrder.TotalQuantity)
                    {
                        makerOrder.Status = OrderStatus.Filled;
                        bestLevel.Dequeue();
                    }
                    else
                    {
                        makerOrder.Status = OrderStatus.PartiallyFilled;
                    }
                }

                if (bestLevel.Count == 0)
                {
                    makerSide.Remove(bestPrice);
                }
            }

            if (takerOrder.FilledQuantity > 0 && takerOrder.FilledQuantity < takerOrder.TotalQuantity)
            {
                takerOrder.Status = OrderStatus.PartiallyFilled;
            }
            else if (takerOrder.FilledQuantity == takerOrder.TotalQuantity)
            {
                takerOrder.Status = OrderStatus.Filled;
            }

            if (takerOrder.FilledQuantity < takerOrder.TotalQuantity && takerOrder.Price > 0)
            {
                if (!takerSide.TryGetValue(takerOrder.Price, out var level))
                {
                    level = new PriceLevel(takerOrder.Price);
                    takerSide.Add(takerOrder.Price, level);
                }
                level.Enqueue(takerOrder);
                
                // If the order wasn't fully filled but partial filled, status could be PartiallyFilled, else New.
                if (takerOrder.Status == OrderStatus.New || takerOrder.Status == OrderStatus.PendingNew) 
                {
                    takerOrder.Status = OrderStatus.New;
                }
            }
            else if (takerOrder.FilledQuantity < takerOrder.TotalQuantity && takerOrder.Price == 0)
            {
                takerOrder.Status = OrderStatus.Cancelled;
            }
        }
        
        // Properties to help test
        public int AsksCount => _asks.Count;
        public int BidsCount => _bids.Count;
        public decimal? BestAsk => _asks.Count > 0 ? _asks.First().Key : null;
        public decimal? BestBid => _bids.Count > 0 ? _bids.First().Key : null;
    }
}
