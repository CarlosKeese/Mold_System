using System.Text;

namespace MoldSystem.Core;

/// <summary>
/// Como um item vira nome de pasta.
///
/// A regra é sempre <c>PREFIXO-NÚMERO - Nome legível</c>. As duas metades têm
/// papéis diferentes e é importante não confundi-los:
///
/// - O <b>código</b> (<c>MLD-0019</c>) é a chave. Nunca muda, e é por ele que o
///   programa acha a pasta no disco.
/// - O <b>nome</b> é para gente ler. Pode ser corrigido a qualquer momento sem
///   quebrar nada, justamente porque ninguém procura por ele.
/// </summary>
public static class Codigos
{
    /// <summary>
    /// Orçamento usa cinco dígitos porque o acervo já vinha assim — as pastas
    /// antigas são <c>00048 - Izan - ...</c>, e esse número está impresso nos PDF
    /// que já foram para o cliente. Os demais tipos nascem com quatro.
    /// </summary>
    private static readonly Dictionary<TipoDeItem, (string Prefixo, int Digitos)> Formato = new()
    {
        [TipoDeItem.Cliente] = ("CLI", 4),
        [TipoDeItem.Projeto] = ("PRJ", 4),
        [TipoDeItem.Orcamento] = ("ORC", 5),
        [TipoDeItem.Molde] = ("MLD", 4),
        [TipoDeItem.Produto] = ("PRD", 4),
        [TipoDeItem.Dispositivo] = ("DIS", 4),
    };

    /// <summary>
    /// Teto do texto legível dentro do nome de uma pasta.
    ///
    /// NÃO é estética. O caminho base já gasta ~76 caracteres
    /// (<c>C:\Users\...\KENATEC - Documentos\001 - Orçamentos e Projetos\</c>) e a
    /// árvore mais funda gasta outros ~115 até <c>06 - Try-out e ajustes</c>.
    /// O Windows corta em 260 quando o <c>LongPathsEnabled</c> está desligado —
    /// que é o padrão, e é o caso desta máquina. O que sobra para o arquivo do
    /// Solid Edge é pouco, e nome de montagem costuma ser longo.
    ///
    /// Com 40 por segmento, os três segmentos que o usuário nomeia (cliente,
    /// projeto, molde) somam no máximo 120, e ainda sobram mais de 60 para o
    /// nome do arquivo.
    /// </summary>
    public const int MaximoDoNome = 40;

    /// <summary>
    /// Nomes que o Windows recusa como pasta, herdados dos dispositivos do DOS.
    /// Continuam proibidos hoje, e com ou sem extensão.
    /// </summary>
    private static readonly HashSet<string> Reservados = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Proibidos pelo Windows e pelo SharePoint. O <c>#</c> e o <c>%</c> entram
    /// por causa do SharePoint — esta pasta é sincronizada, e lá eles atrapalham
    /// a URL do arquivo.
    /// </summary>
    private static readonly char[] Proibidos =
        ['<', '>', ':', '"', '/', '\\', '|', '?', '*', '#', '%', '{', '}', '~'];

    public static string Formatar(TipoDeItem tipo, int numero)
    {
        (string prefixo, int digitos) = Formato[tipo];
        return $"{prefixo}-{numero.ToString().PadLeft(digitos, '0')}";
    }

    /// <summary>Lê <c>MLD-0019</c> de volta. <c>null</c> quando não é um código.</summary>
    public static (TipoDeItem Tipo, int Numero)? Interpretar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        int hifen = codigo.IndexOf('-');
        if (hifen <= 0) return null;

        string prefixo = codigo[..hifen].Trim();
        string resto = codigo[(hifen + 1)..].Trim();

        foreach ((TipoDeItem tipo, (string p, _)) in Formato)
        {
            if (string.Equals(p, prefixo, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(resto, out int numero))
            {
                return (tipo, numero);
            }
        }

        return null;
    }

    /// <summary>O nome completo da pasta de um item.</summary>
    public static string NomeDePasta(TipoDeItem tipo, int numero, string nome)
    {
        string limpo = Limpar(nome);
        return limpo.Length == 0
            ? Formatar(tipo, numero)
            : $"{Formatar(tipo, numero)} - {limpo}";
    }

    /// <summary>
    /// Transforma um nome digitado por gente em algo que o Windows e o SharePoint
    /// aceitam como pasta, sem surpresa.
    ///
    /// Acento fica: o Windows aceita, e "Molde Balde 3,2 L" tem de continuar
    /// legível. O que sai é o que de fato quebra.
    /// </summary>
    public static string Limpar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "";

        var sb = new StringBuilder(nome.Length);
        bool espacoPendente = false;

        foreach (char c in nome.Trim())
        {
            // Caractere de controle vira nada; espaço repetido vira um só.
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                espacoPendente = sb.Length > 0;
                continue;
            }

            if (espacoPendente)
            {
                sb.Append(' ');
                espacoPendente = false;
            }

            sb.Append(Array.IndexOf(Proibidos, c) >= 0 ? '-' : c);
        }

        string limpo = sb.ToString();

        if (limpo.Length > MaximoDoNome)
        {
            // Corta na última palavra inteira que couber: "Molde Balde 3,2 L de
            // Parede Fina" cortado no meio de uma palavra fica ilegível.
            limpo = limpo[..MaximoDoNome];
            int ultimoEspaco = limpo.LastIndexOf(' ');
            if (ultimoEspaco > MaximoDoNome / 2) limpo = limpo[..ultimoEspaco];
        }

        // Ponto ou espaço no fim: o Windows apaga em silêncio ao criar, e aí o
        // caminho que o programa guardou deixa de bater com o que existe.
        limpo = limpo.TrimEnd(' ', '.');

        // Nome reservado sozinho não pode; com o código na frente já não seria
        // problema, mas Limpar() é público e pode ser usado solto.
        if (Reservados.Contains(limpo)) limpo += "_";

        return limpo;
    }
}
