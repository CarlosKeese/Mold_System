namespace MoldSystem.Core;

/// <summary>
/// Por que a falha é um valor e não uma exceção.
///
/// Quase tudo que pode dar errado aqui é <em>esperado</em>, não excepcional: o
/// Python não está instalado, o sidecar ainda está carregando o modelo, o texto
/// passou do limite, a amostra de voz tem 2 segundos e o modelo precisa de 7. Se
/// cada um desses virasse exceção, a camada de UI acabaria com um try/catch por
/// botão e um <c>catch (Exception)</c> no fim engolindo o que sobrou.
///
/// Como valor, o compilador cobra: quem chama não consegue ler <see cref="Value"/>
/// sem antes ter olhado <see cref="IsOk"/>. Exceção fica para o que é de fato
/// excepcional — bug, memória, disco morto.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    private Result(bool ok, T? value, Falha? error)
    {
        IsOk = ok;
        _value = value;
        Error = error;
    }

    public bool IsOk { get; }

    public bool IsError => !IsOk;

    /// <summary>A falha, quando <see cref="IsOk"/> é falso; <c>null</c> quando é verdadeiro.</summary>
    public Falha? Error { get; }

    /// <summary>
    /// O valor. Ler isto num resultado de falha é um bug de quem chama, e é por
    /// isso que joga em vez de devolver <c>default</c> em silêncio.
    /// </summary>
    public T Value => IsOk
        ? _value!
        : throw new InvalidOperationException(
            $"Leitura de Value num Result de falha ({Error?.Codigo}). Confira IsOk antes.");

    public static Result<T> Ok(T value) => new(true, value, null);

    public static Result<T> Erro(Falha falha) => new(false, default, falha);

    public static Result<T> Erro(string codigo, string mensagem, string? detalhe = null) =>
        new(false, default, new Falha(codigo, mensagem, detalhe));

    /// <summary>Converte o valor mantendo a falha intacta.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> f) =>
        IsOk ? Result<TOut>.Ok(f(_value!)) : Result<TOut>.Erro(Error!);

    /// <summary>Encadeia outra operação que também pode falhar.</summary>
    public Result<TOut> Then<TOut>(Func<T, Result<TOut>> f) =>
        IsOk ? f(_value!) : Result<TOut>.Erro(Error!);

    /// <summary>Um caminho para cada lado, sem <c>if</c> na chamada.</summary>
    public TOut Match<TOut>(Func<T, TOut> aoDarCerto, Func<Falha, TOut> aoFalhar) =>
        IsOk ? aoDarCerto(_value!) : aoFalhar(Error!);

    public static implicit operator Result<T>(T value) => Ok(value);
}

/// <summary>
/// Uma falha que a interface consegue mostrar sem traduzir nada.
///
/// <paramref name="Codigo"/> é para o código decidir o que fazer (e para o teste
/// afirmar em cima de algo estável); <paramref name="Mensagem"/> é a frase que o
/// usuário lê, em português e sem jargão; <paramref name="Detalhe"/> é o que vai
/// para o log — stack, stderr do Python, corpo do HTTP.
/// </summary>
/// <param name="Codigo">Identificador estável, em UPPER_SNAKE. Ex.: <c>SIDECAR_FORA</c>.</param>
/// <param name="Mensagem">O que dizer ao usuário, já pronto para a tela.</param>
/// <param name="Detalhe">Contexto técnico para o log. Nunca vai para a tela.</param>
public sealed record Falha(string Codigo, string Mensagem, string? Detalhe = null)
{
    public override string ToString() =>
        Detalhe is null ? $"[{Codigo}] {Mensagem}" : $"[{Codigo}] {Mensagem} — {Detalhe}";
}

/// <summary>Versão sem valor, para operações que só podem dar certo ou falhar.</summary>
public readonly struct Result
{
    private Result(bool ok, Falha? error)
    {
        IsOk = ok;
        Error = error;
    }

    public bool IsOk { get; }

    public bool IsError => !IsOk;

    public Falha? Error { get; }

    public static Result Ok() => new(true, null);

    public static Result Erro(Falha falha) => new(false, falha);

    public static Result Erro(string codigo, string mensagem, string? detalhe = null) =>
        new(false, new Falha(codigo, mensagem, detalhe));
}
