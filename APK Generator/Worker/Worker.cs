using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var factory = new ConnectionFactory() { HostName = "localhost" };
using (var connection = factory.CreateConnection())
using (var channel = connection.CreateModel())
{
    channel.QueueDeclare(queue: "apk-generate",
                         durable: true,
                         exclusive: false,
                         autoDelete: false,
                         arguments: null);

    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (model, ea) =>
    {
        var message = Encoding.UTF8.GetString(ea.Body.ToArray());
        Console.WriteLine(" [x] Received {0}", message);
        
        var args = message.Split(' '); // sépare le message en arguments
        var path = args[0];
        var script = args[1];
        
        // Exécute la commande "yarn run" pour générer l'APK avec le chemin et le script spécifiés en paramètres
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C cd {path} && yarn run {script}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Console.WriteLine(output);
        
        // Renvoie le fichier APK généré en réponse
        var apkFile = $"{path}\\app-release.apk";
        if (File.Exists(apkFile))
        {
            var fileBytes = File.ReadAllBytes(apkFile);
            channel.BasicPublish(exchange: "",
                                 routingKey: ea.BasicProperties.ReplyTo,
                                 basicProperties: new BasicProperties
                                 {
                                     CorrelationId = ea.BasicProperties.CorrelationId
                                 },
                                 body: fileBytes);
            Console.WriteLine(" [x] Sent apk file");
        }
        else
        {
            Console.WriteLine(" [x] Failed to generate apk file");
        }
    };
    channel.BasicConsume(queue: "apk-generate",
                         autoAck: true,
                         consumer: consumer);
}
