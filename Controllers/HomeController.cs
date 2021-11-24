using System.Collections.Generic;
using System.Diagnostics;
using Pessoas.Models;
using Pessoas.Dao;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Pessoas.Service;

namespace Pessoas.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            using (PessoaService service = new PessoaService()) 
            {
                Pessoa p = service.consulte(11234451760);
                
                return View(p);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Inserir()
        {
            Pessoa p = new Pessoa();
            p.Cpf = 11234451760;
            p.Nome = "Rogick";
            p.Endereco = new Endereco();
            p.Endereco.Logradouro = "Rua Boipeba";
            p.Endereco.Numero = 160;
            p.Endereco.Cep = 21557090;
            p.Endereco.Bairro = "Marechal Hermes";
            p.Endereco.Cidade = "Rio de Janeiro";
            p.Endereco.Estado = "RJ";
            p.Telefones = new List<Telefone> {
                new Telefone {Ddd = 21, Numero = 30168859, Tipo = new TipoTelefone(1, "Telefone") },
                new Telefone {Ddd = 21, Numero = 996743188, Tipo = new TipoTelefone(2, "Celular") }
            };

            using (PessoaService service = new PessoaService()) 
            {
                service.insira(p);
                return View(p);
            };
        }

        public IActionResult Alterar()
        {
            using (PessoaService service = new PessoaService()) 
            {
                Pessoa p = service.consulte(11234451760);
                p.Nome = "Rogick Alves Manoel";
                p.Endereco = new Endereco();
                p.Endereco.Logradouro = "Rua Boipeba";
                p.Endereco.Numero = 160;
                p.Endereco.Cep = 21557090;
                p.Endereco.Bairro = "Marechal Hermes";
                p.Endereco.Cidade = "Rio de Janeiro";
                p.Endereco.Estado = "RJ";
                p.Telefones.Clear();
                p.Telefones.Add(new Telefone {Ddd = 21, Numero = 30168859, Tipo = new TipoTelefone(1, "Telefone")});

                service.altere(p);
                return View(p);
            }
        }

        public IActionResult Excluir()
        {
            using (PessoaService service = new PessoaService())
            {
                Pessoa p = service.consulte(11234451760);
                service.exclua(p);
            
                return View();
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
