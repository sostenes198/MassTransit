using System;
using System.Threading.Tasks;
using FireOnWheels.Messaging;

namespace FireOnWheels.Teste
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var bus = BusConfigurator.ConfigureBus();
            
            var sendToUri = new Uri($"{RabbitMqConstants.RabbitMqUri}{RabbitMqConstants.SagaQueue}");
            var endPoint = await bus.GetSendEndpoint(sendToUri);

            await endPoint.Send<IRegisterOrderCommand>(new
            {
                PickupName = "model.PickupName",
                PickupAddress = "model.PickupAddress",
                PickupCity = "model.PickupCity",
                DeliverName = "model.DeliverName",
                DeliverAddress = "model.DeliverAddress",
                DeliverCity = "model.DeliverCity",
                Weight = 100,
                Fragile =  false,
                Oversized = false
            });
        }
    }
}