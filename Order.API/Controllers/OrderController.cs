using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Order.API.DTOs;
using Order.API.Models;
using Shared;

namespace Order.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {

        private readonly AppDbContext _context;
        private readonly IPublishEndpoint  _publishEndpoint;
        public OrderController(AppDbContext context , IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
        }


        [HttpPost]
        public async Task<IActionResult> Create(OrderCreateDTO dto)
        {
            var newOrder = new Models.Order
            {
                BuyerId = dto.BuyerId,
                Status = OrderStatus.Suspend,
                Address = new Address {Line=dto.Address.Line , Disrict = dto.Address.Disrict , Province = dto.Address.Province },
                CreatedDate = DateTime.Now
            };
            dto.orderItems.ForEach(item =>
            {
                newOrder.Items.Add(new OrderItem()
                {
                    Price = item.Price,
                    ProductId = item.ProductId,
                    Count = item.Count
                });
            });
            await _context.AddAsync(newOrder);
            await _context.SaveChangesAsync();

            var orderCreatedEvent = new OrderCreatedEvent()
            {
                BuyerId = dto.BuyerId,
                OrderId = newOrder.Id,
                Payment = new PaymentMessage()
                {
                    CardName = dto.Payment.CardName,
                    CardNumber = dto.Payment.CardNumber,
                    CVV = dto.Payment.Expiration,
                    Expiration = dto.Payment.Expiration,
                    TotalPrice = dto.orderItems.Sum(item => item.Price*item.Count) 
                }
            };
            dto.orderItems.ForEach(item =>
            {
                orderCreatedEvent.orderItemMessages.Add(new OrderItemMessage 
                { 
                    Count = item.Count, 
                    ProductId = item.ProductId 
                });
            });

            await _publishEndpoint.Publish(orderCreatedEvent);

            return Ok();
        }
    }
}
