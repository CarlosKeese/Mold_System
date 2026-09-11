using System.Text.RegularExpressions;

namespace MoldSystem.Core;

/// <summary>O que o migrador entendeu do nome de uma pasta antiga.</summary>
/// <param name="Origem">A pasta como está hoje.</param>
/// <param name="Numero">O número do orçamento, quando o nome trazia um.</param>
/// <param name="Cliente">O cliente inferido; vazio quando não deu para saber.</param>
/// <param name="Descricao">O que sobrou do nome depois de tirar número, cliente e status.</param>
/// <param name="Situacao">Status que estava entre parênteses no nome.</param>
/// <param name="Confianca">O quanto dá para confiar nessa leitura.</param>
public sealed record Leitura(
    string Origem,
    int? Numero,
    string Cliente,
    string Descricao,
    Situacao? Situacao,
    Confianca Confianca)
{
    public string NomeDaPasta => Path.GetFileName(Origem);
}

/// <summary>
/// O quanto a inferência merece crédito.
///
/// Existe porque o acervo tem três convenções misturadas, e forçar todas no
/// mesmo molde produziria cliente chamado "Sem Numereção". O que não dá para ler
/// vai para a quarentena em vez de ir para o lugar errado.
/// </summary>
public enum Confianca
{
    /// <summary>Não deu para inferir cliente. Vai para a quarentena.</summary>
    Nenhuma,

    /// <summary>Deu para inferir, mas com palpite. Vale conferir.</summary>
    Baixa,

    /// <summary>Número, cliente e descrição separados com clareza.</summary>
    Alta,
}

/// <summary>
/// Lê os nomes das pastas do acervo e propõe para onde cada uma vai.
///
/// AS TRÊS CONVENÇÕES QUE CONVIVEM HOJE
/// ------------------------------------
/// <code>
/// 00048 - Izan - Molde Balde 3,2 L (Pedido)   número + cliente + descrição + status
/// 00003 - Precision                            número + cliente
/// 230701 - MOLDE NVPRO                         data AAMMDD + descrição
/// Precision                                    só o cliente
/// Pure Shower - Manutenção                     cliente + descrição
/// Pré-Orçamentos                               nem cliente, nem projeto
/// </code>
///
/// O separador é sempre " - ", e o cliente é sempre a primeira parte depois do
/// número. Essa é a única regularidade de que dá para tirar proveito — e é por
/// isso que o resultado vem com <see cref="Confianca"/> em vez de fingir certeza.
/// </summary>
public static class Migrador
{
    /// <summary>Um número de cinco dígitos ou mais no começo: o do orçamento.</summary>
    private static readonly Regex NumeroNaFrente = new(@"^(\d{3,6})\s*-\s*(.*)$", RegexOptions.Compiled);

    /// <summary>Status entre parênteses no fim do nome.</summary>
    private static readonly Regex StatusNoFim = new(@"^(.*?)\s*\(([^)]+)\)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Pastas que não são cliente nem projeto e não devem virar um. Ficam onde
    /// estão, ou vão para a quarentena.
    /// </summary>
    private static readonly HashSet<string> NaoSaoCliente = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pré-Orçamentos", "Pre-Orçamentos", "Pre-Orcamentos",
        "Sem Numereção", "Sem Numeração", "Sem numero", "Sem número",
        EstruturaDePastas.Quarentena,
    };

    private static readonly Dictionary<string, Situacao> Status = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pedido"] = Situacao.Pedido,
        ["Entregue"] = Situacao.Entregue,
        ["Cancelado"] = Situacao.Cancelado,
        ["Perdido"] = Situacao.Perdido,
        ["Em projeto"] = Situacao.EmProjeto,
        ["Orçado"] = Situacao.Orcado,
        ["Orcado"] = Situacao.Orcado,
    };

    /// <summary>Lê uma pasta do acervo.</summary>
    public static Leitura Ler(string caminho)
    {
        string nome = Path.GetFileName(caminho.TrimEnd(Path.DirectorySeparatorChar));
        string resto = nome;
        int? numero = null;
        Situacao? situacao = null;

        // 1. status entre parênteses, no fim
        Match st = StatusNoFim.Match(resto);
        if (st.Success && Status.TryGetValue(st.Groups[2].Value.Trim(), out Situacao s))
        {
            situacao = s;
            resto = st.Groups[1].Value.Trim();
        }

        // 2. número na frente
        Match num = NumeroNaFrente.Match(resto);
        if (num.Success)
        {
            string digitos = num.Groups[1].Value;

            // "230701" é data (AAMMDD), não número de orçamento. Seis dígitos
            // começando por 2 e com mês plausível: trata como data e descarta.
            if (!PareceData(digitos)) numero = int.Parse(digitos);

            resto = num.Groups[2].Value.Trim();
        }

        // 3. o que sobrou: "Cliente - Descrição" ou só uma das duas
        string[] partes = resto.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        string cliente = partes.Length > 0 ? partes[0] : "";
        string descricao = partes.Length > 1 ? string.Join(" - ", partes[1..]) : "";

        // "00067 - Moldes Frascos" — a primeira parte é descrição, não cliente.
        // Quem começa com palavra de produto quase nunca é nome de empresa.
        if (cliente.Length > 0 && PareceDescricao(cliente))
        {
            descricao = string.IsNullOrEmpty(descricao) ? cliente : $"{cliente} - {descricao}";
            cliente = "";
        }

        Confianca confianca =
            NaoSaoCliente.Contains(nome) || NaoSaoCliente.Contains(cliente) || cliente.Length == 0
                ? Confianca.Nenhuma
                : (numero is not null && descricao.Length > 0 ? Confianca.Alta : Confianca.Baixa);

        if (NaoSaoCliente.Contains(cliente)) cliente = "";

        return new Leitura(caminho, numero, cliente, descricao, situacao, confianca);
    }

    /// <summary>Seis dígitos que se leem como AAMMDD de um ano plausível.</summary>
    private static bool PareceData(string digitos)
    {
        if (digitos.Length != 6) return false;

        int mes = int.Parse(digitos.Substring(2, 2));
        int dia = int.Parse(digitos.Substring(4, 2));
        return mes is >= 1 and <= 12 && dia is >= 1 and <= 31;
    }

    /// <summary>
    /// Palavras que denunciam descrição de trabalho no lugar onde deveria estar o
    /// cliente. Não é infalível — e é por isso que o resultado vira confiança
    /// baixa, não certeza.
    /// </summary>
    private static readonly string[] PalavrasDeTrabalho =
    [
        "molde", "moldes", "ferramenta", "gabarito", "dispositivo", "peça", "peca",
        "análise", "analise", "manutenção", "manutencao", "régua", "regua",
        "fivela", "cadeira", "ducha", "cotovelo", "carretel", "espelho", "placa",
        "anel", "roldana", "roldanas", "tampa", "tampas", "balde", "calço", "calco",
    ];

    private static bool PareceDescricao(string texto)
    {
        string primeira = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return PalavrasDeTrabalho.Contains(primeira, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lê todas as pastas de primeiro nível e agrupa por cliente — que é a forma
    /// como a migração precisa olhar: um cliente com quatro pastas antigas vira
    /// um cliente com quatro projetos, não quatro clientes.
    /// </summary>
    public static Result<IReadOnlyList<Leitura>> LerAcervo(string raiz)
    {
        if (!Directory.Exists(raiz))
        {
            return Result<IReadOnlyList<Leitura>>.Erro(
                "RAIZ_NAO_ENCONTRADA", $"Não achei a pasta «{raiz}».");
        }

        try
        {
            List<Leitura> leituras = [];
            foreach (string pasta in Directory.EnumerateDirectories(raiz))
            {
                string nome = Path.GetFileName(pasta);

                // Não relê o que a própria estrutura nova criou.
                if (nome.StartsWith('_') || Codigos.Interpretar(nome.Split(' ')[0]) is not null)
                {
                    continue;
                }

                leituras.Add(Ler(pasta));
            }

            return Result<IReadOnlyList<Leitura>>.Ok(
                [.. leituras.OrderBy(l => l.Cliente).ThenBy(l => l.Numero ?? int.MaxValue)]);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Leitura>>.Erro(
                "LEITURA_FALHOU", $"Não consegui ler «{raiz}».", ex.Message);
        }
    }

    /// <summary>
    /// Segunda passada: procura, no que ficou sem cliente, um cliente já
    /// conhecido das outras pastas.
    ///
    /// POR QUE ISTO EXISTE
    /// -------------------
    /// A primeira passada assume <c>número - cliente - descrição</c>. Rodando
    /// contra o acervo real apareceu uma quarta convenção, invertida:
    ///
    /// <code>
    /// 00059 - Régua - Marcos
    /// 00061 - Molde Espelho Olho de Gato - Izan
    /// </code>
    ///
    /// Aqui o cliente está no FIM. A guarda de "palavra de trabalho" acerta ao
    /// recusar "Régua" como nome de empresa, mas sozinha ela só joga a pasta na
    /// quarentena — perde o "Marcos" que está logo ali.
    ///
    /// O sinal que resolve é o conjunto: "Izan" e "Marcos" aparecem como cliente
    /// em OUTRAS pastas, lidas com confiança. Um nome que já é cliente em algum
    /// lugar do acervo, aparecendo no fim de outra pasta, quase certamente é o
    /// cliente dela também. Fica em confiança baixa, não alta — é inferência.
    /// </summary>
    public static IReadOnlyList<Leitura> Reconciliar(IReadOnlyList<Leitura> leituras)
    {
        HashSet<string> conhecidos = leituras
            .Where(l => l.Cliente.Length > 0)
            .Select(l => Chave(l.Cliente))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Como o nome canônico aparece escrito: "Marcos (Toninho)" e não "marcos".
        Dictionary<string, string> canonico = leituras
            .Where(l => l.Cliente.Length > 0)
            .GroupBy(l => Chave(l.Cliente), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Cliente, StringComparer.OrdinalIgnoreCase);

        List<Leitura> saida = [];

        foreach (Leitura l in leituras)
        {
            if (l.Confianca != Confianca.Nenhuma || l.Descricao.Length == 0)
            {
                saida.Add(l);
                continue;
            }

            string[] partes = l.Descricao.Split(
                " - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            // De trás para a frente: a convenção invertida põe o cliente no fim.
            int achou = -1;
            for (int i = partes.Length - 1; i >= 0; i--)
            {
                if (conhecidos.Contains(Chave(partes[i]))) { achou = i; break; }
            }

            if (achou < 0)
            {
                saida.Add(l);
                continue;
            }

            string cliente = canonico[Chave(partes[achou])];
            string resto = string.Join(" - ", partes.Where((_, i) => i != achou));

            saida.Add(l with
            {
                Cliente = cliente,
                Descricao = resto,
                Confianca = Confianca.Baixa,
            });
        }

        return saida;
    }

    /// <summary>
    /// Como dois nomes de cliente são comparados: sem o parêntese e sem espaço
    /// sobrando. É o que faz "Marcos" casar com "Marcos (Toninho)".
    /// </summary>
    private static string Chave(string nome)
    {
        int par = nome.IndexOf('(');
        return (par > 0 ? nome[..par] : nome).Trim();
    }

    /// <summary>Agrupa as leituras por cliente, para virar cadastro.</summary>
    public static IReadOnlyDictionary<string, List<Leitura>> PorCliente(IEnumerable<Leitura> leituras) =>
        leituras
            .Where(l => l.Confianca != Confianca.Nenhuma)
            .GroupBy(l => l.Cliente, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
}
