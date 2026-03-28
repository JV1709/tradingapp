using System;
using MatchingEngine;
using Model.Domain;
using Xunit;

namespace MatchingEngine.Tests
{
    public class OrderBookTests
    {
        [Fact]
        public void OrderBook_SortsBidsAndAsksCorrectly()
        {
            var book = new OrderBook("AAPL");

            // Add Buy orders (Bids)
            book.AddOrder(new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Buy, Price = 100, TotalQuantity = 10, AccountKey = "A1" });
            book.AddOrder(new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Buy, Price = 105, TotalQuantity = 10, AccountKey = "A1" });
            
            // Add Sell orders (Asks)
            book.AddOrder(new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Sell, Price = 110, TotalQuantity = 10, AccountKey = "A1" });
            book.AddOrder(new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Sell, Price = 108, TotalQuantity = 10, AccountKey = "A1" });

            // Bids are descending, highest first
            Assert.Equal(105, book.BestBid);
            
            // Asks are ascending, lowest first
            Assert.Equal(108, book.BestAsk);
        }

        [Fact]
        public void OrderBook_MatchesCrossingOrders()
        {
            var book = new OrderBook("AAPL");

            var sellOrder = new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Sell, Price = 100, TotalQuantity = 15, AccountKey = "A1" };
            book.AddOrder(sellOrder);

            var buyOrder = new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Buy, Price = 105, TotalQuantity = 10, AccountKey = "A2" };
            book.AddOrder(buyOrder);

            Assert.Equal(10, buyOrder.FilledQuantity);
            Assert.Equal(OrderStatus.Filled, buyOrder.Status);

            Assert.Equal(10, sellOrder.FilledQuantity);
            Assert.Equal(OrderStatus.PartiallyFilled, sellOrder.Status);
            
            Assert.Equal(100, book.BestAsk); // Still 5 remaining at 100
        }

        [Fact]
        public void OrderBook_IgnoresCancelledOrders()
        {
            var book = new OrderBook("AAPL");

            var sellOrder1 = new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Sell, Price = 100, TotalQuantity = 10, AccountKey = "A1" };
            var sellOrder2 = new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Sell, Price = 105, TotalQuantity = 10, AccountKey = "A1" };
            
            book.AddOrder(sellOrder1);
            book.AddOrder(sellOrder2);

            // Cancel the best ask
            book.CancelOrder(sellOrder1.OrderId);

            var buyOrder = new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Buy, Price = 110, TotalQuantity = 10, AccountKey = "A2" };
            book.AddOrder(buyOrder);

            // It should have skipped sellOrder1 because of the cancellation cache
            Assert.Equal(0, sellOrder1.FilledQuantity);
            Assert.Equal(10, sellOrder2.FilledQuantity);
            Assert.Equal(10, buyOrder.FilledQuantity);
            
            // Asks should be empty now since sellOrder1 was removed during lazy deletion, and sellOrder2 was filled
            Assert.Null(book.BestAsk);
        }

        [Fact]
        public void OrderBook_MatchesMarketOrders()
        {
            var book = new OrderBook("AAPL");
            var sellOrder = new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Sell, Price = 100, TotalQuantity = 15, AccountKey = "A1" };
            book.AddOrder(sellOrder);

            var marketBuyOrder = new Order { OrderId = Guid.NewGuid(), Symbol = "AAPL", Side = Side.Buy, Price = 0, TotalQuantity = 10, AccountKey = "A2" };
            book.AddOrder(marketBuyOrder);

            Assert.Equal(10, marketBuyOrder.FilledQuantity);
            Assert.Equal(OrderStatus.Filled, marketBuyOrder.Status);
        }
    }
}
