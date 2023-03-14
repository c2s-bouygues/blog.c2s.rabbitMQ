using System.Text;
using RabbitMQ.Client;

var factory = new ConnectionFactory() { HostName = "localhost" };
using (var connection = factory.CreateConnection())
using (var channel = connection.CreateModel())
{
    channel.QueueDeclare(queue: "apk-generate",
                         durable: true, // Permet la persistance des messages
                         exclusive: false,
                         autoDelete: false,
                         arguments: null);

    var path = args[0];
    var script = args[1];
    var message = $"{path} generate apk using yarn run {script}";
    var body = Encoding.UTF8.GetBytes(message);
    channel.BasicPublish(exchange: "",
                         routingKey: "apk-generate",
                         basicProperties: null,
                         body: body);
    Console.WriteLine(" [x] Sent {0}", message);
}