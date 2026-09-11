using MoldSystem.Core;

// Linha de comando do Mold System. Existe antes da interface gráfica porque a
// migração do acervo é uma operação de uma vez só, que precisa ser SIMULADA,
// conferida e só então executada — e isso se faz melhor num terminal, onde a
// saída inteira fica registrada, do que numa janela.

const string RaizPadrao =
    @"C:\Users\CarlosKeese\Kenatec\KENATEC - Documentos\001 - Orçamentos e Projetos";

string comando = args.Length > 0 ? args[0].ToLowerInvariant() : "ajuda";
string raiz = Argumento(args, "--raiz") ?? RaizPadrao;
string banco = Argumento(args, "--banco")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MoldSystem", "moldsystem.db");

switch (comando)
{
    case "sondar":
        return Sondar(raiz);
    case "simular":
        return Migrar(raiz, banco, simular: true);
    case "migrar":
        return Migrar(raiz, banco, simular: false);
    case "desfazer":
        return Desfazer(raiz, banco);
    default:
        Console.WriteLine("""
            Mold System — organização de clientes, projetos, moldes e produtos.

              sondar     lê o acervo e mostra o que entendeu de cada pasta
              simular    mostra a migração inteira SEM tocar em disco
              migrar     executa a migração, gravando log de desfazer
              desfazer   devolve cada pasta para onde estava

            Opções:
              --raiz <pasta>    onde está o acervo
              --banco <arquivo> onde fica o banco (padrão: %LOCALAPPDATA%\MoldSystem)
            """);
        return 0;
}

static string? Argumento(string[] args, string nome)
{
    int i = Array.IndexOf(args, nome);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static Result<IReadOnlyList<Leitura>> LerTudo(string raiz)
{
    Result<IReadOnlyList<Leitura>> r = Migrador.LerAcervo(raiz);
    return r.IsError ? r : Result<IReadOnlyList<Leitura>>.Ok(Migrador.Reconciliar(r.Value));
}

static int Sondar(string raiz)
{
    Result<IReadOnlyList<Leitura>> r = LerTudo(raiz);
    if (r.IsError) { Console.Error.WriteLine(r.Error); return 1; }

    foreach (Confianca nivel in new[] { Confianca.Alta, Confianca.Baixa, Confianca.Nenhuma })
    {
        var doNivel = r.Value.Where(l => l.Confianca == nivel).ToList();
        Console.WriteLine($"--- confiança {nivel} ({doNivel.Count}) ---");
        foreach (Leitura l in doNivel)
        {
            Console.WriteLine($"  {l.NomeDaPasta}");
            Console.WriteLine($"      cliente='{l.Cliente}'  descrição='{l.Descricao}'");
        }
        Console.WriteLine();
    }

    return 0;
}

static int Migrar(string raiz, string banco, bool simular)
{
    Result<IReadOnlyList<Leitura>> leituras = LerTudo(raiz);
    if (leituras.IsError) { Console.Error.WriteLine(leituras.Error); return 1; }

    using var catalogo = new Catalogo(banco, raiz);
    var executor = new ExecutorDaMigracao(catalogo, raiz);

    Console.WriteLine(simular ? "SIMULAÇÃO — nada será movido." : "MIGRANDO de verdade.");
    Console.WriteLine($"Acervo: {raiz}");
    Console.WriteLine($"Banco:  {banco}");
    Console.WriteLine();

    Relatorio rel = executor.Executar(leituras.Value, simular);

    foreach (Movimento m in rel.Movimentos)
    {
        Console.WriteLine($"  {Path.GetFileName(m.De)}");
        Console.WriteLine($"    -> {m.Para.Replace(raiz, "…")}");
    }

    Console.WriteLine();
    Console.WriteLine($"Clientes:   {rel.ClientesCriados}");
    Console.WriteLine($"Projetos:   {rel.ProjetosCriados}");
    Console.WriteLine($"Movimentos: {rel.Movimentos.Count}");
    Console.WriteLine($"Quarentena: {rel.Ignorados.Count}");

    if (rel.Falhas.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"FALHAS ({rel.Falhas.Count}):");
        foreach (Falha f in rel.Falhas) Console.WriteLine($"  {f}");
    }

    if (!simular)
    {
        Console.WriteLine();
        Console.WriteLine($"Log de desfazer: {executor.CaminhoDoLog}");
        Console.WriteLine("Para reverter tudo:  MoldSystem.Cli desfazer");
    }

    return rel.Limpo ? 0 : 2;
}

static int Desfazer(string raiz, string banco)
{
    using var catalogo = new Catalogo(banco, raiz);
    var executor = new ExecutorDaMigracao(catalogo, raiz);

    Result<int> r = executor.Desfazer();
    if (r.IsError) { Console.Error.WriteLine(r.Error); return 1; }

    Console.WriteLine($"Pastas devolvidas ao lugar de origem: {r.Value}");
    Console.WriteLine("O banco NÃO foi apagado — apague o arquivo se quiser recomeçar do zero.");
    return 0;
}
