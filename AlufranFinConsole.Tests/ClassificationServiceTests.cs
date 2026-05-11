using AlufranFinConsole.Application.Services;
using AlufranFinConsole.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace AlufranFinConsole.Tests;

/// <summary>
/// Testa a ClassificationService com DbContext in-memory.
/// </summary>
public class ClassificationServiceTests
{
    private static IApplicationDbContext BuildContext(Action<IApplicationDbContext>? seed = null)
    {
        var ctx = Substitute.For<IApplicationDbContext>();
        // Simula lista vazia como padrão
        var emptyCategories = new List<ErpCategory>().AsQueryable();
        var emptyRules      = new List<ClassificationRule>().AsQueryable();

        ctx.ErpCategories.Returns(MockDbSet(emptyCategories));
        ctx.ClassificationRules.Returns(MockDbSet(emptyRules));

        seed?.Invoke(ctx);
        return ctx;
    }

    private static DbSet<T> MockDbSet<T>(IQueryable<T> data) where T : class
    {
        var asyncData = new TestAsyncEnumerable<T>(data);
        var mock = Substitute.For<DbSet<T>, IQueryable<T>, IAsyncEnumerable<T>>();
        ((IQueryable<T>)mock).Provider.Returns(asyncData.Provider);
        ((IQueryable<T>)mock).Expression.Returns(data.Expression);
        ((IQueryable<T>)mock).ElementType.Returns(data.ElementType);
        ((IQueryable<T>)mock).GetEnumerator().Returns(data.GetEnumerator());
        ((IAsyncEnumerable<T>)mock)
            .GetAsyncEnumerator(Arg.Any<CancellationToken>())
            .Returns(_ => asyncData.GetAsyncEnumerator());
        return mock;
    }

    private readonly ITextNormalizationService _norm = new TextNormalizationService();

    [Fact]
    public async Task ClassifyByErpCategory_Succeeds()
    {
        var cat = new ErpCategory
        {
            Id = 1, Code = "ALIM", Name = "Alimentos",
            ErpCategoryKey = "ALIMENTOS",
            DreGroup = "CUSTO", DreSubgroup = "CMV", DreOrder = 10,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };

        var categories = new List<ErpCategory> { cat }.AsQueryable();
        var ctx = Substitute.For<IApplicationDbContext>();
        var catSet   = MockDbSet(categories);
        var ruleSet  = MockDbSet(new List<ClassificationRule>().AsQueryable());
        ctx.ErpCategories.Returns(catSet);
        ctx.ClassificationRules.Returns(ruleSet);

        var sut    = new ClassificationService(ctx, _norm);
        var row    = new Dictionary<string, string> { ["ErpCategoryName"] = "Alimentos" };
        var result = await sut.ClassifyAsync(row, "COMP");

        result.Classified.Should().BeTrue();
        result.DreGroup.Should().Be("CUSTO");
        result.ErpCategoryId.Should().Be(1);
    }

    [Fact]
    public async Task UnclassifiedCategory_ReturnsFalse()
    {
        var ctx = Substitute.For<IApplicationDbContext>();
        var emptyCategories = MockDbSet(new List<ErpCategory>().AsQueryable());
        var emptyRules      = MockDbSet(new List<ClassificationRule>().AsQueryable());
        ctx.ErpCategories.Returns(emptyCategories);
        ctx.ClassificationRules.Returns(emptyRules);

        var sut    = new ClassificationService(ctx, _norm);
        var row    = new Dictionary<string, string> { ["ErpCategoryName"] = "CATEGORIA DESCONHECIDA" };
        var result = await sut.ClassifyAsync(row, "PAG");

        result.Classified.Should().BeFalse();
        result.UnclassifiedReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ClassifyByRule_Succeeds()
    {
        var rule = new ClassificationRule
        {
            Id = 1, Name = "ORION → CUSTO",
            Priority = 1, RuleType = "FixedSupplier", BaseType = "PAG",
            Condition = "SupplierName=ORION REFEICOES",
            DreGroup = "DESPESA", DreSubgroup = "PESSOAL", DreOrder = 50,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };

        var ctx = Substitute.For<IApplicationDbContext>();
        var noCategories = MockDbSet(new List<ErpCategory>().AsQueryable());
        var ruleSet2     = MockDbSet(new List<ClassificationRule> { rule }.AsQueryable());
        ctx.ErpCategories.Returns(noCategories);
        ctx.ClassificationRules.Returns(ruleSet2);

        var sut    = new ClassificationService(ctx, _norm);
        var row    = new Dictionary<string, string> { ["SupplierName"] = "ORION REFEICOES" };
        var result = await sut.ClassifyAsync(row, "PAG");

        result.Classified.Should().BeTrue();
        result.DreGroup.Should().Be("DESPESA");
    }

    /// <summary>
    /// Prova de precedência: FixedSupplier (Nível 1, Priority=10) deve vencer
    /// ErpCategory (Nível 2) quando ambos estão disponíveis.
    /// </summary>
    [Fact]
    public async Task SpecificSupplierRule_WinsOverErpCategory()
    {
        var cat = new ErpCategory
        {
            Id = 1, Code = "ALIM", Name = "Alimentos", ErpCategoryKey = "ALIMENTOS",
            DreGroup = "CUSTO", DreSubgroup = "CMV", DreOrder = 10,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        // Regra de fornecedor específico com prioridade mais alta (número menor)
        var ruleSupplier = new ClassificationRule
        {
            Id = 1, Name = "ORION específico", Priority = 10,
            RuleType = "FixedSupplier", BaseType = "*",
            Condition = "SupplierName=ORION",
            DreGroup = "DESPESA_INTERNA", DreSubgroup = "INTERCO", DreOrder = 5,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };

        var ctx = Substitute.For<IApplicationDbContext>();
        var catSet3   = MockDbSet(new List<ErpCategory> { cat }.AsQueryable());
        var ruleSet3  = MockDbSet(new List<ClassificationRule> { ruleSupplier }.AsQueryable());
        ctx.ErpCategories.Returns(catSet3);
        ctx.ClassificationRules.Returns(ruleSet3);

        var sut = new ClassificationService(ctx, _norm);
        // Linha tem tanto ErpCategoryName (→ CUSTO) quanto SupplierName específico (→ DESPESA_INTERNA)
        var row = new Dictionary<string, string>
        {
            ["SupplierName"]    = "ORION",
            ["ErpCategoryName"] = "Alimentos"
        };
        var result = await sut.ClassifyAsync(row, "PAG");

        // FixedSupplier (Priority=10, Nível 1) deve ganhar sobre ErpCategory (Nível 2)
        result.Classified.Should().BeTrue();
        result.DreGroup.Should().Be("DESPESA_INTERNA",
            because: "regra de fornecedor específico tem precedência sobre categoria ERP");
    }

    /// <summary>
    /// Linha não classificada NÃO gera FinancialFact (invariante central do pipeline).
    /// </summary>
    [Fact]
    public async Task UnclassifiedLine_DoesNotProduceFinancialFact()
    {
        var ctx = Substitute.For<IApplicationDbContext>();
        var catSet4  = MockDbSet(new List<ErpCategory>().AsQueryable());
        var ruleSet4 = MockDbSet(new List<ClassificationRule>().AsQueryable());
        ctx.ErpCategories.Returns(catSet4);
        ctx.ClassificationRules.Returns(ruleSet4);

        var sut    = new ClassificationService(ctx, _norm);
        var row    = new Dictionary<string, string> { ["SupplierName"] = "FORNECEDOR SEM REGRA" };
        var result = await sut.ClassifyAsync(row, "PAG");

        // Invariante: Classified=false → FinancialFact NÃO deve ser gerado
        result.Classified.Should().BeFalse();
        result.UnclassifiedReason.Should().NotBeNullOrEmpty();
        // DreGroup nulo confirma que não há dados para gerar FinancialFact
        result.DreGroup.Should().BeNull();
    }

    /// <summary>
    /// Condições compostas com ponto-e-vírgula (AND lógico).
    /// </summary>
    [Fact]
    public async Task ClassifyByRule_CompositeCondition_Succeeds()
    {
        var rule = new ClassificationRule
        {
            Id = 1, Name = "ORION + ALIMENTOS",
            Priority = 20, RuleType = "FixedSupplier", BaseType = "PAG",
            Condition = "SupplierName=ORION;ErpCategoryName=ALIMENTOS",
            DreGroup = "CUSTO", DreSubgroup = "CMV", DreOrder = 10,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };

        var ctx = Substitute.For<IApplicationDbContext>();
        var catSet5  = MockDbSet(new List<ErpCategory>().AsQueryable());
        var ruleSet5 = MockDbSet(new List<ClassificationRule> { rule }.AsQueryable());
        ctx.ErpCategories.Returns(catSet5);
        ctx.ClassificationRules.Returns(ruleSet5);

        var sut = new ClassificationService(ctx, _norm);

        // Deve classificar quando AMBAS as condições batem
        var rowMatch = new Dictionary<string, string>
            { ["SupplierName"] = "ORION", ["ErpCategoryName"] = "ALIMENTOS" };
        var resultMatch = await sut.ClassifyAsync(rowMatch, "PAG");
        resultMatch.Classified.Should().BeTrue();
        resultMatch.DreGroup.Should().Be("CUSTO");

        // Não deve classificar quando só uma condição bate
        var rowNoMatch = new Dictionary<string, string>
            { ["SupplierName"] = "ORION", ["ErpCategoryName"] = "OUTRO" };
        var resultNoMatch = await sut.ClassifyAsync(rowNoMatch, "PAG");
        resultNoMatch.Classified.Should().BeFalse();
    }
}

// ── Helpers para IQueryable assíncrono em testes ──────────────────────────────
// Garante que toda a cadeia LINQ (Where → OrderBy → ToListAsync / FirstOrDefaultAsync)
// mantém IAsyncEnumerable<T> — necessário para os métodos Async do EF Core.

/// <summary>
/// Wrapper que expõe um IQueryable como IAsyncEnumerable, preservando o provider
/// assíncrono em toda a cadeia LINQ.
/// </summary>
public class TestAsyncEnumerable<T> : IAsyncEnumerable<T>, IQueryable<T>, IOrderedQueryable<T>
{
    private readonly IQueryable<T> _inner;

    public TestAsyncEnumerable(IQueryable<T> inner) => _inner = inner;

    // Cada vez que o provider é acessado, retornamos um TestAsyncQueryProvider
    // para que qualquer LINQ posterior (Where/OrderBy/…) também use este wrapper.
    public IQueryProvider Provider   => new TestAsyncQueryProvider<T>(_inner.Provider);
    public Type           ElementType => _inner.ElementType;
    public System.Linq.Expressions.Expression Expression   => _inner.Expression;

    public IEnumerator<T>                    GetEnumerator() => _inner.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
        => new TestAsyncEnumerator<T>(_inner.GetEnumerator());
}

public class TestAsyncQueryProvider<TEntity> : IQueryProvider, IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;
    public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
        => _inner.CreateQuery(expression);

    // Encapsula o resultado em TestAsyncEnumerable para que IAsyncEnumerable
    // continue disponível após cada operação LINQ encadeada.
    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
        => new TestAsyncEnumerable<TElement>(_inner.CreateQuery<TElement>(expression));

    public object? Execute(System.Linq.Expressions.Expression expression)
        => _inner.Execute(expression);

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
        => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(
        System.Linq.Expressions.Expression expression, CancellationToken ct = default)
    {
        var result = Execute(expression);
        var resultType = typeof(TResult).GetGenericArguments()[0];
        var task = typeof(Task).GetMethod(nameof(Task.FromResult))!
                               .MakeGenericMethod(resultType)
                               .Invoke(null, [result]);
        return (TResult)task!;
    }
}

public class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;
    public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());
    public ValueTask DisposeAsync() { inner.Dispose(); return default; }
}
