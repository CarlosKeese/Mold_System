namespace MoldSystem.Core;

/// <summary>
/// Os seis tipos de coisa que o sistema cataloga, e o prefixo do código de cada
/// uma.
///
/// O prefixo entra no nome da pasta (<c>MLD-0019 - Molde Balde 3,2 L</c>) e é o
/// que torna o nome legível **e** estável ao mesmo tempo: o programa acha a
/// pasta pelo código, então o texto depois do hífen pode ser corrigido à vontade
/// sem quebrar referência nenhuma.
/// </summary>
public enum TipoDeItem
{
    Cliente,
    Projeto,
    Orcamento,
    Molde,
    Produto,
    Dispositivo,
}

/// <summary>Em que pé está o trabalho.</summary>
/// <remarks>
/// Vive só no banco, nunca no nome da pasta. Renomear pasta dentro do OneDrive
/// re-sincroniza tudo que está lá dentro e invalida caminho salvo — inclusive os
/// links entre montagem e peça do Solid Edge, que são o tipo de quebra que só
/// aparece semanas depois, na hora de abrir o molde.
/// </remarks>
public enum Situacao
{
    /// <summary>Orçado, aguardando resposta do cliente.</summary>
    Orcado,

    /// <summary>Cliente aprovou; é trabalho.</summary>
    Pedido,

    EmProjeto,
    EmUsinagem,
    EmTryout,
    Entregue,
    Cancelado,

    /// <summary>Sem resposta do cliente há tempo demais para continuar contando como ativo.</summary>
    Perdido,
}

/// <summary>O que toda entidade catalogada tem.</summary>
public abstract class Item
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>O número sequencial do tipo. Vira <c>MLD-0019</c> no nome da pasta.</summary>
    public int Numero { get; set; }

    /// <summary>Como a coisa se chama, em português e para gente ler.</summary>
    public string Nome { get; set; } = "";

    public string? Observacoes { get; set; }

    public DateTimeOffset CriadoEm { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset AtualizadoEm { get; set; } = DateTimeOffset.Now;

    public abstract TipoDeItem Tipo { get; }

    /// <summary>O código curto e estável: <c>MLD-0019</c>.</summary>
    public string Codigo => Codigos.Formatar(Tipo, Numero);

    /// <summary>O nome da pasta: <c>MLD-0019 - Molde Balde 3,2 L</c>.</summary>
    public string NomeDaPasta => Codigos.NomeDePasta(Tipo, Numero, Nome);

    public override string ToString() => $"{Codigo} — {Nome}";
}

/// <summary>
/// Quem paga. É a raiz da árvore: tudo que existe pertence a um cliente.
/// </summary>
public sealed class Cliente : Item
{
    public override TipoDeItem Tipo => TipoDeItem.Cliente;

    /// <summary>Nome curto, do dia a dia ("Precision"). É o que vai na pasta.</summary>
    public string? RazaoSocial { get; set; }

    public string? Cnpj { get; set; }

    public string? Contato { get; set; }

    public string? Email { get; set; }

    public string? Telefone { get; set; }

    public string? Cidade { get; set; }
}

/// <summary>
/// O trabalho que agrupa entregas. Um projeto pode conter vários moldes, vários
/// dispositivos e referenciar vários produtos.
///
/// Existe **sempre**, mesmo para um molde só: um caminho previsível vale mais do
/// que uma pasta a menos, e o dia em que o cliente pedir o segundo molde não
/// exige mover nada de lugar.
/// </summary>
public sealed class Projeto : Item
{
    public override TipoDeItem Tipo => TipoDeItem.Projeto;

    public required Guid ClienteId { get; set; }

    public Situacao Situacao { get; set; } = Situacao.Orcado;

    /// <summary>
    /// Produtos do cliente que este projeto trabalha.
    ///
    /// É aqui que mora o "muitos para muitos" que o sistema de arquivos não sabe
    /// representar: o produto tem UM lugar no disco, sob o cliente, e a ligação
    /// com os projetos vive no banco.
    /// </summary>
    public List<Guid> ProdutoIds { get; set; } = [];

    public DateOnly? PrazoDeEntrega { get; set; }
}

/// <summary>
/// O documento comercial e suas revisões.
///
/// **Não é pasta de trabalho.** Guarda o PDF, o arquivo de diagramação e o
/// histórico de revisões — nada mais. É o que resolve "um molde pode ser orçado
/// várias vezes": cinco orçamentos, cinco pastas <c>ORC-</c>, e um único
/// <c>MLD-</c> onde o trabalho de verdade acontece.
/// </summary>
public sealed class Orcamento : Item
{
    public override TipoDeItem Tipo => TipoDeItem.Orcamento;

    public required Guid ClienteId { get; set; }

    /// <summary>O projeto que este orçamento cobre, quando já existe um.</summary>
    public Guid? ProjetoId { get; set; }

    /// <summary>
    /// O que está sendo orçado: moldes, produtos, dispositivos, em qualquer
    /// combinação. Um orçamento pode cobrir os três ao mesmo tempo.
    /// </summary>
    public List<Guid> ItensIds { get; set; } = [];

    public Situacao Situacao { get; set; } = Situacao.Orcado;

    /// <summary>Revisão atual: 0 = original, 1 = "Rev_A", e assim por diante.</summary>
    public int Revisao { get; set; }

    public decimal? Valor { get; set; }

    public DateOnly? Data { get; set; }

    /// <summary>
    /// O número que o orçamento tinha antes do sistema existir — o "00048" das
    /// pastas antigas. Preservado porque é por ele que o cliente cobra e é ele
    /// que está impresso nos PDF já enviados.
    /// </summary>
    public string? NumeroLegado { get; set; }
}

/// <summary>A peça plástica que o molde produz.</summary>
/// <remarks>
/// Pertence ao <b>cliente</b>, não ao projeto. É isso que permite o mesmo
/// produto entrar em vários projetos do mesmo cliente sem nenhuma cópia de
/// arquivo — e sem que ninguém precise lembrar qual pasta tem a versão boa.
/// </remarks>
public sealed class Produto : Item
{
    public override TipoDeItem Tipo => TipoDeItem.Produto;

    public required Guid ClienteId { get; set; }

    public string? Material { get; set; }

    public double? PesoGramas { get; set; }

    /// <summary>Quantas peças por ciclo, quando já se sabe.</summary>
    public int? Cavidades { get; set; }
}

/// <summary>O molde.</summary>
public sealed class Molde : Item
{
    public override TipoDeItem Tipo => TipoDeItem.Molde;

    public required Guid ClienteId { get; set; }

    public required Guid ProjetoId { get; set; }

    /// <summary>O produto que este molde injeta, quando há um cadastrado.</summary>
    public Guid? ProdutoId { get; set; }

    public Situacao Situacao { get; set; } = Situacao.Orcado;

    public int? Cavidades { get; set; }

    /// <summary>Duas placas, três placas, câmara quente…</summary>
    public string? TipoDeMolde { get; set; }

    public string? Aco { get; set; }
}

/// <summary>Gabarito, dispositivo de montagem, ferramenta de corte — o que não é molde nem produto.</summary>
public sealed class Dispositivo : Item
{
    public override TipoDeItem Tipo => TipoDeItem.Dispositivo;

    public required Guid ClienteId { get; set; }

    public required Guid ProjetoId { get; set; }

    public Situacao Situacao { get; set; } = Situacao.Orcado;
}
