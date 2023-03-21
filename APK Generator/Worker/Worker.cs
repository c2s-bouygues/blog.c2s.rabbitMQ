using System.IO.Compression;
using System.Diagnostics;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

var factory = new ConnectionFactory() { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(queue: "apk-to-generate", durable: true, exclusive: false, autoDelete: false, arguments: null);
channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
var consumer = new EventingBasicConsumer(channel);
channel.BasicConsume(queue: "apk-to-generate",
                    autoAck: false,
                    consumer: consumer);
Console.WriteLine(" [x] Awaiting RPC requests");
    
consumer.Received += (model, ea) =>
{
    // Propriétés du message
    string response = string.Empty;
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);    
    
    var props = ea.BasicProperties;
    var replyProps = channel.CreateBasicProperties();
    replyProps.CorrelationId = props.CorrelationId;

    // Sépare le message en arguments
    var args = message.Split(' ');
    var path = args[0];
    var script = args[1];
    var extractPath = @$"/C/{path}";

    // Décompression du fichier zip
    byte[] fileBytes = ea.Body.ToArray();
    File.WriteAllBytes(path, fileBytes);
    ZipFile.ExtractToDirectory(path, extractPath);

    try{
        Console.WriteLine(" [x] Received {0}", message);
        // Exécute la commande yarn install pour installer les dépendances puis "yarn run" pour générer l'APK avec le chemin et le script spécifiés en paramètres
        var apkprocess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C cd {extractPath} && yarn install && yarn run {script}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        apkprocess.Start();
        string output = apkprocess.StandardOutput.ReadToEnd();
        apkprocess.WaitForExit();
        Console.WriteLine(output);
    }

    // Gestion d'erreur
    catch (Exception e)
    {
        Console.WriteLine($" [.] {e.Message}");
        response = string.Empty;
    }

    finally
    {
        // Renvoie du fichier APK généré en réponse
        var apkFile = $"{path}\\app-release.apk";

        if (File.Exists(apkFile))
        {
            var fileB = File.ReadAllBytes(apkFile);
            channel.BasicPublish(exchange: "", routingKey: props.ReplyTo, basicProperties: replyProps, body: fileB);
            channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            Console.WriteLine(" [x] Sent apk file");
        }

        else
        {
            Console.WriteLine(" [x] Failed to generate apk file");
        }
    }    
};
Console.WriteLine(" Press [enter] to exit.");
Console.ReadLine();
