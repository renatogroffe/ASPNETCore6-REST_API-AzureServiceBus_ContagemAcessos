using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using APIContagem.Models;

namespace APIContagem.Controllers;

[ApiController]
[Route("[controller]")]
public class ContadorController : ControllerBase
{
    private static readonly Contador _CONTADOR = new Contador();
    private readonly ILogger<ContadorController> _logger;
    private readonly IConfiguration _configuration;

    public ContadorController(ILogger<ContadorController> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ResultadoContador> Get()
    {
        int valorAtualContador;
        lock (_CONTADOR)
        {
            _CONTADOR.Incrementar();
            valorAtualContador = _CONTADOR.ValorAtual;
        }

        var resultado = new ResultadoContador()
        {
            ValorAtual = valorAtualContador,
            Producer = _CONTADOR.Local,
            Kernel = _CONTADOR.Kernel,
            Framework = _CONTADOR.Framework,
            Mensagem = _configuration["MensagemVariavel"]
        };

        string queueName = _configuration["AzureServiceBus:Queue"];
        string jsonContagem = JsonSerializer.Serialize(resultado);

        QueueClient? client = null;
        try
        {
            client = new QueueClient(
                _configuration["AzureServiceBus:ConnectionString"],
                queueName, ReceiveMode.ReceiveAndDelete);
            await client.SendAsync(
                new Message(Encoding.UTF8.GetBytes(jsonContagem)));
            _logger.LogInformation(
                    $"Azure Service Bus - Envio para a fila {queueName} concluído | " +
                    $"{jsonContagem}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exceção: {ex.GetType().FullName} | " +
                            $"Mensagem: {ex.Message}");
        }
        finally
        {
            if (client is not null)
                await client.CloseAsync();
        }
 
        return resultado;
    }
}