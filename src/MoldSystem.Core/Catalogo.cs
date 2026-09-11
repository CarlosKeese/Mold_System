using LiteDB;

namespace MoldSystem.Core;

/// <summary>
/// O catálogo: cadastra e, no mesmo movimento, cria a pasta.
///
/// É essa junção que dá razão ao programa existir. Cadastro sem pasta deixa o
/// disco desorganizado; pasta sem cadastro deixa o sistema cego. As duas coisas
/// acontecem juntas ou nenhuma acontece.
///
/// A ORDEM IMPORTA: pasta primeiro, banco depois
/// ---------------------------------------------
/// Se o banco gravasse primeiro e a criação da pasta falhasse (caminho longo
/// demais, OneDrive offline, permissão), ficaria um cadastro apontando para uma
/// pasta que não existe — e o usuário só descobriria ao tentar salvar arquivo
/// lá. Fazendo a pasta primeiro, uma falha simplesmente aborta o cadastro e nada
/// fica pela metade.
/// </summary>
public sealed class Catalogo : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly EstruturaDePastas _pastas;

    public Catalogo(string caminhoDoBanco, string raizDasPastas)
    {
        string? dir = Path.GetDirectoryName(caminhoDoBanco);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _db = new LiteDatabase(caminhoDoBanco);
        _pastas = new EstruturaDePastas(raizDasPastas);

        Clientes.EnsureIndex(x => x.Numero, unique: true);
        Projetos.EnsureIndex(x => x.ClienteId);
        Orcamentos.EnsureIndex(x => x.ClienteId);
        Moldes.EnsureIndex(x => x.ProjetoId);
        Produtos.EnsureIndex(x => x.ClienteId);
        Dispositivos.EnsureIndex(x => x.ProjetoId);
    }

    public EstruturaDePastas Pastas => _pastas;

    public ILiteCollection<Cliente> Clientes => _db.GetCollection<Cliente>("clientes");

    public ILiteCollection<Projeto> Projetos => _db.GetCollection<Projeto>("projetos");

    public ILiteCollection<Orcamento> Orcamentos => _db.GetCollection<Orcamento>("orcamentos");

    public ILiteCollection<Molde> Moldes => _db.GetCollection<Molde>("moldes");

    public ILiteCollection<Produto> Produtos => _db.GetCollection<Produto>("produtos");

    public ILiteCollection<Dispositivo> Dispositivos => _db.GetCollection<Dispositivo>("dispositivos");

    // --- cadastro ------------------------------------------------------------

    public Result<Cliente> CadastrarCliente(string nome, Action<Cliente>? ajustes = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Cliente>.Erro("NOME_VAZIO", "O cliente precisa de um nome.");
        }

        var c = new Cliente { Numero = ProximoNumero(Clientes), Nome = nome.Trim() };
        ajustes?.Invoke(c);

        Result<string> pasta = _pastas.Criar(c);
        if (pasta.IsError) return Result<Cliente>.Erro(pasta.Error!);

        Clientes.Insert(c);
        return Result<Cliente>.Ok(c);
    }

    public Result<Projeto> CadastrarProjeto(Guid clienteId, string nome, Action<Projeto>? ajustes = null)
    {
        Cliente? c = Clientes.FindById(clienteId);
        if (c is null) return Result<Projeto>.Erro("CLIENTE_NAO_ENCONTRADO", "Esse cliente não existe.");

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Projeto>.Erro("NOME_VAZIO", "O projeto precisa de um nome.");
        }

        var p = new Projeto
        {
            ClienteId = clienteId,
            Numero = ProximoNumero(Projetos),
            Nome = nome.Trim(),
        };
        ajustes?.Invoke(p);

        Result<string> pasta = _pastas.Criar(c, p);
        if (pasta.IsError) return Result<Projeto>.Erro(pasta.Error!);

        Projetos.Insert(p);
        return Result<Projeto>.Ok(p);
    }

    /// <summary>
    /// Cadastra um orçamento. <paramref name="numeroLegado"/> preserva o número
    /// antigo ("00048") quando o orçamento vem do acervo — é por ele que o
    /// cliente cobra, e ele está impresso nos PDF já enviados.
    /// </summary>
    public Result<Orcamento> CadastrarOrcamento(
        Guid clienteId, string nome, string? numeroLegado = null, Action<Orcamento>? ajustes = null)
    {
        Cliente? c = Clientes.FindById(clienteId);
        if (c is null) return Result<Orcamento>.Erro("CLIENTE_NAO_ENCONTRADO", "Esse cliente não existe.");

        var o = new Orcamento
        {
            ClienteId = clienteId,
            Numero = NumeroDeOrcamento(numeroLegado),
            Nome = (nome ?? "").Trim(),
            NumeroLegado = numeroLegado,
        };
        ajustes?.Invoke(o);

        Result<string> pasta = _pastas.Criar(c, o);
        if (pasta.IsError) return Result<Orcamento>.Erro(pasta.Error!);

        Orcamentos.Insert(o);
        return Result<Orcamento>.Ok(o);
    }

    public Result<Produto> CadastrarProduto(Guid clienteId, string nome, Action<Produto>? ajustes = null)
    {
        Cliente? c = Clientes.FindById(clienteId);
        if (c is null) return Result<Produto>.Erro("CLIENTE_NAO_ENCONTRADO", "Esse cliente não existe.");

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Produto>.Erro("NOME_VAZIO", "O produto precisa de um nome.");
        }

        var p = new Produto
        {
            ClienteId = clienteId,
            Numero = ProximoNumero(Produtos),
            Nome = nome.Trim(),
        };
        ajustes?.Invoke(p);

        Result<string> pasta = _pastas.Criar(c, p);
        if (pasta.IsError) return Result<Produto>.Erro(pasta.Error!);

        Produtos.Insert(p);
        return Result<Produto>.Ok(p);
    }

    public Result<Molde> CadastrarMolde(Guid projetoId, string nome, Action<Molde>? ajustes = null)
    {
        Projeto? p = Projetos.FindById(projetoId);
        if (p is null) return Result<Molde>.Erro("PROJETO_NAO_ENCONTRADO", "Esse projeto não existe.");

        Cliente? c = Clientes.FindById(p.ClienteId);
        if (c is null) return Result<Molde>.Erro("CLIENTE_NAO_ENCONTRADO", "O cliente do projeto sumiu.");

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Molde>.Erro("NOME_VAZIO", "O molde precisa de um nome.");
        }

        var m = new Molde
        {
            ClienteId = c.Id,
            ProjetoId = projetoId,
            Numero = ProximoNumero(Moldes),
            Nome = nome.Trim(),
        };
        ajustes?.Invoke(m);

        Result<string> pasta = _pastas.Criar(c, p, m);
        if (pasta.IsError) return Result<Molde>.Erro(pasta.Error!);

        Moldes.Insert(m);
        return Result<Molde>.Ok(m);
    }

    public Result<Dispositivo> CadastrarDispositivo(
        Guid projetoId, string nome, Action<Dispositivo>? ajustes = null)
    {
        Projeto? p = Projetos.FindById(projetoId);
        if (p is null) return Result<Dispositivo>.Erro("PROJETO_NAO_ENCONTRADO", "Esse projeto não existe.");

        Cliente? c = Clientes.FindById(p.ClienteId);
        if (c is null) return Result<Dispositivo>.Erro("CLIENTE_NAO_ENCONTRADO", "O cliente do projeto sumiu.");

        if (string.IsNullOrWhiteSpace(nome))
        {
            return Result<Dispositivo>.Erro("NOME_VAZIO", "O dispositivo precisa de um nome.");
        }

        var d = new Dispositivo
        {
            ClienteId = c.Id,
            ProjetoId = projetoId,
            Numero = ProximoNumero(Dispositivos),
            Nome = nome.Trim(),
        };
        ajustes?.Invoke(d);

        Result<string> pasta = _pastas.Criar(c, p, d);
        if (pasta.IsError) return Result<Dispositivo>.Erro(pasta.Error!);

        Dispositivos.Insert(d);
        return Result<Dispositivo>.Ok(d);
    }

    // --- consultas -----------------------------------------------------------

    public IReadOnlyList<Cliente> ListarClientes() =>
        [.. Clientes.FindAll().OrderBy(x => x.Nome)];

    public IReadOnlyList<Projeto> ProjetosDe(Guid clienteId) =>
        [.. Projetos.Find(x => x.ClienteId == clienteId).OrderByDescending(x => x.Numero)];

    public IReadOnlyList<Molde> MoldesDe(Guid projetoId) =>
        [.. Moldes.Find(x => x.ProjetoId == projetoId).OrderBy(x => x.Numero)];

    public IReadOnlyList<Produto> ProdutosDe(Guid clienteId) =>
        [.. Produtos.Find(x => x.ClienteId == clienteId).OrderBy(x => x.Nome)];

    public IReadOnlyList<Orcamento> OrcamentosDe(Guid clienteId) =>
        [.. Orcamentos.Find(x => x.ClienteId == clienteId).OrderByDescending(x => x.Numero)];

    /// <summary>
    /// Os projetos que usam este produto. É a consulta que o sistema de arquivos
    /// nunca poderia responder, e a razão de o banco existir ao lado das pastas.
    /// </summary>
    public IReadOnlyList<Projeto> ProjetosQueUsam(Guid produtoId) =>
        [.. Projetos.FindAll().Where(p => p.ProdutoIds.Contains(produtoId))];

    /// <summary>Todos os orçamentos já feitos para um mesmo molde.</summary>
    public IReadOnlyList<Orcamento> OrcamentosDoItem(Guid itemId) =>
        [.. Orcamentos.FindAll().Where(o => o.ItensIds.Contains(itemId)).OrderBy(o => o.Numero)];

    // --- numeração -----------------------------------------------------------

    private static int ProximoNumero<T>(ILiteCollection<T> col) where T : Item =>
        col.Count() == 0 ? 1 : col.FindAll().Max(x => x.Numero) + 1;

    /// <summary>
    /// Um orçamento migrado fica com o número que já tinha; um novo continua a
    /// sequência. Isso mantém "00048" sendo 00048 para sempre.
    /// </summary>
    private int NumeroDeOrcamento(string? numeroLegado)
    {
        if (numeroLegado is not null && int.TryParse(numeroLegado, out int antigo) && antigo > 0)
        {
            bool livre = Orcamentos.Count(x => x.Numero == antigo) == 0;
            if (livre) return antigo;
        }

        return ProximoNumero(Orcamentos);
    }

    public void Dispose() => _db.Dispose();
}
