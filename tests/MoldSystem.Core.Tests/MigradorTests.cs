using MoldSystem.Core;
using Xunit;

namespace MoldSystem.Core.Tests;

/// <summary>
/// Os casos vêm dos nomes REAIS das 44 pastas do acervo, lidos em 2026-09-10.
/// Inventar exemplos aqui não serviria de nada: o valor do migrador é acertar
/// justamente as irregularidades que ninguém planejou.
/// </summary>
public class MigradorTests
{
    [Fact]
    public void NumeroClienteDescricaoEStatus_saemSeparados()
    {
        var l = Migrador.Ler(@"C:\x\00048 - Izan - Molde Balde 3,2 L (Pedido)");

        Assert.Equal(48, l.Numero);
        Assert.Equal("Izan", l.Cliente);
        Assert.Equal("Molde Balde 3,2 L", l.Descricao);
        Assert.Equal(Situacao.Pedido, l.Situacao);
        Assert.Equal(Confianca.Alta, l.Confianca);
    }

    [Fact]
    public void NumeroECliente_semDescricao_ficaEmConfiancaBaixa()
    {
        var l = Migrador.Ler(@"C:\x\00003 - Precision");

        Assert.Equal(3, l.Numero);
        Assert.Equal("Precision", l.Cliente);
        Assert.Equal("", l.Descricao);
        Assert.Equal(Confianca.Baixa, l.Confianca);
    }

    [Fact]
    public void SoONomeDoCliente_ehLidoComoCliente()
    {
        var l = Migrador.Ler(@"C:\x\ELO CORREIAS");

        Assert.Null(l.Numero);
        Assert.Equal("ELO CORREIAS", l.Cliente);
        Assert.Equal(Confianca.Baixa, l.Confianca);
    }

    [Fact]
    public void ClienteEDescricao_semNumero()
    {
        var l = Migrador.Ler(@"C:\x\Pure Shower - Manutenção");

        Assert.Equal("Pure Shower", l.Cliente);
        Assert.Equal("Manutenção", l.Descricao);
    }

    [Fact]
    public void StatusEntregue_ehReconhecido()
    {
        var l = Migrador.Ler(@"C:\x\00042 - Precision - Molde Ferramenta (Entregue)");

        Assert.Equal(Situacao.Entregue, l.Situacao);
        Assert.Equal("Precision", l.Cliente);
        Assert.Equal("Molde Ferramenta", l.Descricao);
    }

    [Fact]
    public void DataNaFrente_naoViraNumeroDeOrcamento()
    {
        // "230701" é 2023-07-01, não o orçamento 230701. Tratar como número
        // criaria um ORC-230701 e furaria a sequência para sempre.
        var l = Migrador.Ler(@"C:\x\230701 - MOLDE NVPRO");

        Assert.Null(l.Numero);
    }

    [Fact]
    public void DescricaoNoLugarDoCliente_naoViraCliente()
    {
        // "Moldes Frascos" é o trabalho, não a empresa. Sem essa guarda o
        // acervo ganharia um cliente chamado "Moldes".
        var l = Migrador.Ler(@"C:\x\00067 - Moldes Frascos");

        Assert.Equal("", l.Cliente);
        Assert.Contains("Moldes Frascos", l.Descricao);
        Assert.Equal(Confianca.Nenhuma, l.Confianca);
    }

    [Theory]
    [InlineData(@"C:\x\Pré-Orçamentos")]
    [InlineData(@"C:\x\00000 - Sem Numereção")]
    public void PastasDeDespejo_naoViramCliente(string caminho)
    {
        var l = Migrador.Ler(caminho);

        Assert.Equal(Confianca.Nenhuma, l.Confianca);
        Assert.Equal("", l.Cliente);
    }

    [Fact]
    public void OMesmoCliente_emVariasPastas_ehAgrupado()
    {
        // É o ponto central da migração: a Precision aparece em sete pastas do
        // acervo. Tem de virar UM cliente com sete projetos, não sete clientes.
        Leitura[] leituras =
        [
            Migrador.Ler(@"C:\x\00003 - Precision"),
            Migrador.Ler(@"C:\x\00009 - Precision"),
            Migrador.Ler(@"C:\x\00042 - Precision - Molde Ferramenta (Entregue)"),
            Migrador.Ler(@"C:\x\00045 - Precision - Ferramenta Snap-on"),
            Migrador.Ler(@"C:\x\00048 - Izan - Molde Balde 3,2 L (Pedido)"),
        ];

        var porCliente = Migrador.PorCliente(leituras);

        Assert.Equal(2, porCliente.Count);
        Assert.Equal(4, porCliente["Precision"].Count);
        Assert.Single(porCliente["Izan"]);
    }

    [Fact]
    public void PorCliente_deixaDeForaOQueNaoTemCliente()
    {
        Leitura[] leituras =
        [
            Migrador.Ler(@"C:\x\Pré-Orçamentos"),
            Migrador.Ler(@"C:\x\00048 - Izan - Molde Balde"),
        ];

        var porCliente = Migrador.PorCliente(leituras);

        Assert.Single(porCliente);
        Assert.True(porCliente.ContainsKey("Izan"));
    }

    [Fact]
    public void ClienteComCaixaDiferente_ehOMesmoCliente()
    {
        Leitura[] leituras =
        [
            Migrador.Ler(@"C:\x\00003 - Precision"),
            Migrador.Ler(@"C:\x\00009 - PRECISION - Molde"),
        ];

        Assert.Single(Migrador.PorCliente(leituras));
    }
}

/// <summary>
/// A convenção invertida — cliente no FIM — só apareceu ao rodar contra o
/// acervo real. Estes casos são exatamente as pastas que caíam na quarentena.
/// </summary>
public class ReconciliacaoTests
{
    private static IReadOnlyList<Leitura> Ler(params string[] nomes) =>
        Migrador.Reconciliar([.. nomes.Select(n => Migrador.Ler(@"C:\x\" + n))]);

    [Fact]
    public void ClienteNoFim_ehAchadoQuandoJaEhConhecido()
    {
        var r = Ler(
            "00048 - Izan - Molde Balde 3,2 L (Pedido)",
            "00061 - Molde Espelho Olho de Gato - Izan");

        Leitura invertida = r.Single(x => x.NomeDaPasta.StartsWith("00061"));
        Assert.Equal("Izan", invertida.Cliente);
        Assert.Equal("Molde Espelho Olho de Gato", invertida.Descricao);
        Assert.Equal(Confianca.Baixa, invertida.Confianca);
    }

    [Fact]
    public void ClienteDesconhecido_continuaNaQuarentena()
    {
        // "Nelson" não aparece como cliente em nenhuma outra pasta. Chutar seria
        // pior do que deixar para conferência.
        var r = Ler("00062 - Molde Placa - Nelson");

        Assert.Equal(Confianca.Nenhuma, r[0].Confianca);
        Assert.Equal("", r[0].Cliente);
    }

    [Fact]
    public void NomeComParenteses_casaComOMesmoClienteSemParenteses()
    {
        var r = Ler(
            "00050 - Marcos (Toninho) - Cotovelo",
            "00059 - Régua - Marcos");

        Leitura invertida = r.Single(x => x.NomeDaPasta.StartsWith("00059"));
        Assert.Equal("Marcos (Toninho)", invertida.Cliente);
        Assert.Equal("Régua", invertida.Descricao);
    }

    [Fact]
    public void Reconciliar_naoMexeNoQueJaEstavaBom()
    {
        var r = Ler("00048 - Izan - Molde Balde 3,2 L (Pedido)");

        Assert.Equal(Confianca.Alta, r[0].Confianca);
        Assert.Equal("Izan", r[0].Cliente);
        Assert.Equal("Molde Balde 3,2 L", r[0].Descricao);
    }
}
