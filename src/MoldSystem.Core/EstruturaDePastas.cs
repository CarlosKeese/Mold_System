namespace MoldSystem.Core;

/// <summary>
/// A taxonomia de pastas, num lugar só.
///
/// O DESENHO, E O PORQUÊ DE CADA DECISÃO
/// -------------------------------------
/// <code>
/// 001 - Orçamentos e Projetos/
/// └── CLI-0007 - Precision/
///     ├── 00 - Cadastro/                 contrato, NDA, contatos, tabela de preços
///     ├── 01 - Orçamentos/
///     │   └── ORC-00048 - Molde Balde/   só o documento e suas revisões
///     ├── 02 - Produtos/
///     │   └── PRD-0031 - Balde 3,2 L/    do CLIENTE, não do projeto
///     └── 03 - Projetos/
///         └── PRJ-0012 - Linha de Baldes/
///             ├── 01 - Recebido do cliente/
///             ├── 02 - Moldes/
///             │   └── MLD-0019 - Molde Balde/
///             │       ├── 01 - Projeto 3D/
///             │       ├── 02 - Desenhos/
///             │       ├── 03 - Eletrodos/
///             │       ├── 04 - CAM/
///             │       ├── 05 - Compras/
///             │       └── 06 - Try-out e ajustes/
///             ├── 03 - Dispositivos/
///             └── 04 - Documentos/
/// </code>
///
/// <b>Orçamento não é pasta de trabalho.</b> Um molde pode ser orçado cinco
/// vezes; se o trabalho morasse sob o orçamento, existiriam cinco cópias do
/// mesmo molde e ninguém saberia qual é a boa. O orçamento guarda o documento;
/// o trabalho vive no projeto.
///
/// <b>Produto pende do cliente, não do projeto.</b> O mesmo produto entra em
/// vários projetos do mesmo cliente. Se pendesse do projeto, seria cópia — e
/// cópia de modelo 3D é a origem clássica de usinar a revisão errada.
///
/// <b>O grafo não cabe aqui, e tudo bem.</b> Sistema de arquivos é árvore; os
/// dados são rede. Cada coisa tem UM lugar canônico no disco, e as ligações
/// (produto ↔ projeto, orçamento ↔ itens) vivem no banco. Nada de atalho nem de
/// junção: esta pasta é sincronizada por OneDrive/SharePoint, onde link
/// simbólico dá problema de sincronização e duplicação.
/// </summary>
public sealed class EstruturaDePastas
{
    /// <summary>As subpastas fixas de um cliente.</summary>
    public static readonly string[] DoCliente =
        ["00 - Cadastro", "01 - Orçamentos", "02 - Produtos", "03 - Projetos"];

    /// <summary>As subpastas fixas de um projeto.</summary>
    public static readonly string[] DoProjeto =
        ["01 - Recebido do cliente", "02 - Moldes", "03 - Dispositivos", "04 - Documentos"];

    /// <summary>
    /// As subpastas de um molde, na ordem do fluxo real de trabalho: chega o 3D,
    /// sai desenho, saem eletrodos, vai para o CAM, compra-se o que falta, e por
    /// fim ajusta-se no try-out.
    /// </summary>
    public static readonly string[] DoMolde =
    [
        "01 - Projeto 3D",
        "02 - Desenhos",
        "03 - Eletrodos",
        "04 - CAM",
        "05 - Compras",
        "06 - Try-out e ajustes",
    ];

    /// <summary>As subpastas de um produto.</summary>
    public static readonly string[] DoProduto =
        ["01 - Recebido do cliente", "02 - Modelo 3D", "03 - Análise"];

    /// <summary>Onde vai o que a migração não conseguiu classificar.</summary>
    public const string Quarentena = "_A classificar";

    public EstruturaDePastas(string raiz) => Raiz = raiz;

    /// <summary>A pasta que contém tudo. Ex.: <c>...\001 - Orçamentos e Projetos</c>.</summary>
    public string Raiz { get; }

    // --- caminhos ------------------------------------------------------------

    public string Do(Cliente c) => Path.Combine(Raiz, c.NomeDaPasta);

    public string CadastroDo(Cliente c) => Path.Combine(Do(c), DoCliente[0]);

    public string OrcamentosDo(Cliente c) => Path.Combine(Do(c), DoCliente[1]);

    public string ProdutosDo(Cliente c) => Path.Combine(Do(c), DoCliente[2]);

    public string ProjetosDo(Cliente c) => Path.Combine(Do(c), DoCliente[3]);

    public string Do(Cliente c, Orcamento o) => Path.Combine(OrcamentosDo(c), o.NomeDaPasta);

    public string Do(Cliente c, Produto p) => Path.Combine(ProdutosDo(c), p.NomeDaPasta);

    public string Do(Cliente c, Projeto p) => Path.Combine(ProjetosDo(c), p.NomeDaPasta);

    public string MoldesDe(Cliente c, Projeto p) => Path.Combine(Do(c, p), DoProjeto[1]);

    public string DispositivosDe(Cliente c, Projeto p) => Path.Combine(Do(c, p), DoProjeto[2]);

    public string Do(Cliente c, Projeto p, Molde m) => Path.Combine(MoldesDe(c, p), m.NomeDaPasta);

    public string Do(Cliente c, Projeto p, Dispositivo d) =>
        Path.Combine(DispositivosDe(c, p), d.NomeDaPasta);

    // --- criação -------------------------------------------------------------

    /// <summary>
    /// Cria a pasta do cliente com as quatro subpastas fixas.
    ///
    /// Idempotente de propósito: cadastrar de novo, ou rodar depois de alguém ter
    /// criado a pasta à mão, não pode dar erro nem apagar nada.
    /// </summary>
    public Result<string> Criar(Cliente c) => CriarArvore(Do(c), DoCliente);

    public Result<string> Criar(Cliente c, Orcamento o) => CriarArvore(Do(c, o), []);

    public Result<string> Criar(Cliente c, Produto p) => CriarArvore(Do(c, p), DoProduto);

    public Result<string> Criar(Cliente c, Projeto p) => CriarArvore(Do(c, p), DoProjeto);

    public Result<string> Criar(Cliente c, Projeto p, Molde m) =>
        CriarArvore(Do(c, p, m), DoMolde);

    public Result<string> Criar(Cliente c, Projeto p, Dispositivo d) =>
        CriarArvore(Do(c, p, d), []);

    private static Result<string> CriarArvore(string pasta, string[] subpastas)
    {
        Result comprimento = ConferirComprimento(pasta, subpastas);
        if (comprimento.IsError) return Result<string>.Erro(comprimento.Error!);

        try
        {
            Directory.CreateDirectory(pasta);
            foreach (string sub in subpastas) Directory.CreateDirectory(Path.Combine(pasta, sub));
            return Result<string>.Ok(pasta);
        }
        catch (Exception ex)
        {
            return Result<string>.Erro(
                "PASTA_NAO_CRIADA", $"Não consegui criar «{pasta}».", ex.Message);
        }
    }

    /// <summary>
    /// Limite do Windows quando <c>LongPathsEnabled</c> está desligado — que é o
    /// padrão, e é o caso da máquina onde isto roda.
    /// </summary>
    private const int LimiteDoWindows = 260;

    /// <summary>Folga reservada para o nome do arquivo que vai morar na pasta mais funda.</summary>
    private const int FolgaParaArquivo = 60;

    /// <summary>
    /// Recusa criar uma árvore que já nasceria perto do limite de caminho.
    ///
    /// Falhar aqui, com o motivo escrito, é muito melhor do que o modo como isso
    /// se manifesta sozinho: o Solid Edge salva a montagem, alguma peça funda
    /// estoura o limite, e o erro que aparece fala de permissão ou de arquivo
    /// não encontrado — nunca de tamanho de caminho.
    /// </summary>
    private static Result ConferirComprimento(string pasta, string[] subpastas)
    {
        int maisFunda = pasta.Length;
        foreach (string sub in subpastas)
        {
            maisFunda = Math.Max(maisFunda, pasta.Length + 1 + sub.Length);
        }

        if (maisFunda + FolgaParaArquivo > LimiteDoWindows)
        {
            return Result.Erro(
                "CAMINHO_LONGO_DEMAIS",
                "O caminho desta pasta ficaria longo demais para o Windows. "
                + "Encurte o nome do cliente, do projeto ou do molde.",
                $"{maisFunda} caracteres + {FolgaParaArquivo} de folga para o arquivo "
                + $"passa do limite de {LimiteDoWindows}. Pasta: «{pasta}»");
        }

        return Result.Ok();
    }
}
