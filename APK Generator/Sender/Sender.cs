using System.IO.Compression;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

class Program
{
    static void Main(string[] args)
    {
        // Chemin vers le dossier 
        var path = args[0];
            
        // Le script yarn à utiliser
        var script = args[1];

        // Chemin et nom de fichier .zip à créer
        string zipPath = $"{path}.zip";

        // Compression du dossier en .zip
        ZipFile.CreateFromDirectory(path, zipPath);

        // Connexion au serveur RabbitMQ
        var factory = new ConnectionFactory() { HostName = "localhost" };
        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            // Nom de la file d'attente RabbitMQ
            string queueName = "apk-to-generate";

            // Lit le contenu du fichier .zip dans un tableau d'octets
            byte[] fileBytes = File.ReadAllBytes(zipPath);

            // Définit les propriétés du message RPC
            var props = channel.CreateBasicProperties();
            props.CorrelationId = Guid.NewGuid().ToString();
            props.ReplyTo = "apk-generated";

            // Envoi du message RPC contenant le fichier .zip dans la file d'attente
            channel.QueueDeclare(queue: queueName,
                                durable: false,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

            channel.BasicPublish(exchange: "",
                                routingKey: queueName,
                                basicProperties: props,
                                body: fileBytes);
            Console.WriteLine("File sent");

            // Attente d'une réponse à la demande RPC
            var consumer = new EventingBasicConsumer(channel);
            channel.BasicConsume(queue: "apk-generated",
                                autoAck: true,
                                consumer: consumer);

            consumer.Received += (model, ea) => {
                byte[] responseBytes = ea.Body.ToArray();
                string responseString = System.Text.Encoding.UTF8.GetString(responseBytes);
                Console.WriteLine("[x] Received {0}", responseString);
            };

            Console.WriteLine("[x] Awaiting Worker response");
            Console.ReadLine();
        }
    }
}
