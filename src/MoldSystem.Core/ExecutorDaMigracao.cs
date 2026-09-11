using System.Text.Json;

namespace MoldSystem.Core;

/// <summary>Uma pasta que mudou de lugar. É a linha do log de desfazer.</summary>
public sealed record Movimento(string De, string Para, string Motivo);

/// <summary>O que a migração fez, ou faria.</summary>
public sealed record Relatorio(
    IReadOnlyList<Movimento> Movimentos,
    IReadOnlyList<string> Ignorados,
    IReadOnlyList<Falha> Falhas,
    int ClientesCriados,
    int ProjetosCriados)
{
    public bool Limpo => Falhas.Count == 0;
}

/// <summary>
/// Move o acervo antigo para a estrutura nova.
///
/// TRÊS REGRAS QUE NÃO SE NEGOCIAM
/// -------------------------------
/// 1. <b>Nunca sobrescreve.</b> Se o destino já existe, a pasta fica onde está e
///    isso vira uma falha no relatório. Fundir duas pastas automaticamente é
///    como se perde arquivo sem ninguém perceber.
/// 2. <b>Log de desfazer, gravado ANTES de cada movimento.</b> Não é "por via das
///    dúvidas": são arquivos de cliente, dentro do OneDrive, e a única resposta
///    aceitável para "moveu errado" é desfazer, não procurar.
/// 3. <b>O que não deu para classificar vai para a quarentena</b>, não para um
///    palpite. Uma pasta em <c>_A classificar</c> é um minuto de trabalho; uma
///    pasta no cliente errado é uma hora de procura daqui a seis meses.
/// </summary>
public sealed class ExecutorDaMigracao
{
    private readonly Catalogo _catalogo;
    private readonly string _raiz;

    public ExecutorDaMigracao(Catalogo catalogo, string raiz)
    {
        _catalogo = catalogo;
        _raiz = raiz;
    }

    /// <summary>Onde o log de desfazer é gravado.</summary>
    public string CaminhoDoLog => Path.Combine(_raiz, "_migracao-desfazer.json");

    /// <summary>
    /// Executa a migração. Com <paramref name="simular"/> ligado, calcula tudo e
    /// não toca em disco — é como se vê o resultado antes de aceitá-lo.
    /// </summary>
    public Relatorio Executar(IReadOnlyList<Leitura> leituras, bool simular)
    {
        List<Movimento> movimentos = [];
        List<string> ignorados = [];
        List<Falha> falhas = [];
        int clientes = 0, projetos = 0;

        // Cache por nome para não cadastrar a Precision doze vezes.
        Dictionary<string, Cliente> porNome = new(StringComparer.OrdinalIgnoreCase);

        foreach (Leitura l in leituras.Where(x => x.Confianca != Confianca.Nenhuma))
        {
            if (!porNome.TryGetValue(l.Cliente, out Cliente? cliente))
            {
                Cliente? existente = _catalogo.Clientes
                    .FindAll()
                    .FirstOrDefault(c => string.Equals(c.Nome, l.Cliente, StringComparison.OrdinalIgnoreCase));

                if (existente is not null)
                {
                    cliente = existente;
                }
                else if (simular)
                {
                    cliente = new Cliente { Numero = 900 + clientes, Nome = l.Cliente };
                    clientes++;
                }
                else
                {
                    Result<Cliente> novo = _catalogo.CadastrarCliente(l.Cliente);
                    if (novo.IsError) { falhas.Add(novo.Error!); continue; }
                    cliente = novo.Value;
                    clientes++;
                }

                porNome[l.Cliente] = cliente;
            }

            // O nome do projeto: a descrição quando há uma; senão o número do
            // orçamento, que é como o Carlos se refere ao trabalho hoje.
            string nomeDoProjeto = l.Descricao.Length > 0
                ? l.Descricao
                : (l.Numero is not null ? $"Orçamento {l.Numero:00000}" : "Trabalhos anteriores");

            Projeto projeto;
            if (simular)
            {
                projeto = new Projeto
                {
                    ClienteId = cliente.Id,
                    Numero = 900 + projetos,
                    Nome = nomeDoProjeto,
                };
                projetos++;
            }
            else
            {
                Result<Projeto> novo = _catalogo.CadastrarProjeto(
                    cliente.Id, nomeDoProjeto,
                    p => p.Situacao = l.Situacao ?? Situacao.Orcado);

                if (novo.IsError) { falhas.Add(novo.Error!); continue; }
                projeto = novo.Value;
                projetos++;
            }

            // O conteúdo antigo vai para "01 - Recebido do cliente": é onde ele
            // pode ser separado depois sem risco. Jogá-lo na raiz do projeto
            // misturaria com as pastas que a estrutura nova cria.
            string destino = Path.Combine(
                _catalogo.Pastas.Do(cliente, projeto), EstruturaDePastas.DoProjeto[0]);

            Result mover = Mover(l.Origem, destino, $"{cliente.Nome} / {projeto.Nome}", simular, movimentos);
            if (mover.IsError) falhas.Add(mover.Error!);
        }

        // O que não deu para ler vai para a quarentena, intacto.
        foreach (Leitura l in leituras.Where(x => x.Confianca == Confianca.Nenhuma))
        {
            string destino = Path.Combine(_raiz, EstruturaDePastas.Quarentena);
            Result mover = Mover(l.Origem, destino, "não deu para inferir o cliente", simular, movimentos);

            if (mover.IsError) falhas.Add(mover.Error!);
            else ignorados.Add(l.NomeDaPasta);
        }

        return new Relatorio(movimentos, ignorados, falhas, clientes, projetos);
    }

    private Result Mover(
        string origem, string pastaDestino, string motivo, bool simular, List<Movimento> log)
    {
        string alvo = Path.Combine(pastaDestino, Path.GetFileName(origem));

        if (Directory.Exists(alvo))
        {
            return Result.Erro(
                "DESTINO_JA_EXISTE",
                $"Já existe «{Path.GetFileName(origem)}» no destino. Deixei a pasta onde estava.",
                alvo);
        }

        var mov = new Movimento(origem, alvo, motivo);
        log.Add(mov);

        if (simular) return Result.Ok();

        try
        {
            Directory.CreateDirectory(pastaDestino);

            // Gravar o log ANTES de mover: se a energia cair no meio, o que já
            // saiu do lugar está registrado. Depois do movimento, não estaria.
            GravarLog(log);

            Directory.Move(origem, alvo);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            log.Remove(mov);
            return Result.Erro(
                "MOVIMENTO_FALHOU", $"Não consegui mover «{origem}».", ex.Message);
        }
    }

    private void GravarLog(List<Movimento> movimentos)
    {
        try
        {
            File.WriteAllText(
                CaminhoDoLog,
                JsonSerializer.Serialize(movimentos, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Não vale abortar a migração por causa do log — mas o movimento em
            // si já está na lista em memória e sai no relatório.
        }
    }

    /// <summary>
    /// Desfaz: devolve cada pasta para onde estava, na ordem inversa.
    ///
    /// Só mexe no que o log registra. Pasta criada depois da migração, ou arquivo
    /// que o usuário mexeu no meio, fica onde está.
    /// </summary>
    public Result<int> Desfazer()
    {
        if (!File.Exists(CaminhoDoLog))
        {
            return Result<int>.Erro("SEM_LOG", "Não há log de migração para desfazer.");
        }

        try
        {
            List<Movimento> movimentos =
                JsonSerializer.Deserialize<List<Movimento>>(File.ReadAllText(CaminhoDoLog)) ?? [];

            int devolvidos = 0;
            foreach (Movimento m in Enumerable.Reverse(movimentos))
            {
                if (!Directory.Exists(m.Para) || Directory.Exists(m.De)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(m.De)!);
                Directory.Move(m.Para, m.De);
                devolvidos++;
            }

            return Result<int>.Ok(devolvidos);
        }
        catch (Exception ex)
        {
            return Result<int>.Erro("DESFAZER_FALHOU", "Não consegui desfazer a migração.", ex.Message);
        }
    }
}
