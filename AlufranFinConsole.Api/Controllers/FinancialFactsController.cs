using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AlufranFinConsole.Application.Services;

namespace AlufranFinConsole.Api.Controllers;

/// <summary>
/// GET /api/financial-facts?competence=YYYY-MM
/// GET /api/dre/competence?competence=YYYY-MM
/// GET /api/dre/cash?competence=YYYY-MM
/// </summary>
[ApiController]
[Authorize]
public class FinancialFactsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public FinancialFactsController(IApplicationDbContext context) => _context = context;

    // GET /api/financial-facts
    [HttpGet("api/financial-facts")]
    public async Task<IActionResult> GetFacts(
        [FromQuery] string? competence,
        [FromQuery] string? baseType,
        [FromQuery] string? dreGroup,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 500,
        CancellationToken ct = default)
    {
        var query = _context.FinancialFacts.AsQueryable();
        if (!string.IsNullOrEmpty(competence)) query = query.Where(f => f.Competence == competence);
        if (!string.IsNullOrEmpty(baseType))   query = query.Where(f => f.BaseType == baseType.ToUpper());
        if (!string.IsNullOrEmpty(dreGroup))   query = query.Where(f => f.DreGroup == dreGroup);

        var total = await query.CountAsync(ct);
        var facts = await query
            .OrderBy(f => f.DreOrder).ThenBy(f => f.DreGroup)
            .Skip(skip).Take(Math.Min(take, 2000))
            .Select(f => new
            {
                f.Id, f.BaseType, f.Competence, f.DocumentNumber,
                f.AmountCompetence, f.AmountCash,
                f.DreGroup, f.DreSubgroup, f.DreOrder, f.ClassificationStatus,
                f.IssueDate, f.PaymentDate, f.ReceiptDate, f.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { total, skip, take = facts.Count, facts });
    }

    // GET /api/dre/competence?competence=YYYY-MM
    [HttpGet("api/dre/competence")]
    public async Task<IActionResult> GetDreCompetence(
        [FromQuery] string competence, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(competence))
            return BadRequest(new { error = "competence é obrigatório (YYYY-MM)" });

        var facts = await _context.FinancialFacts
            .Where(f => f.Competence == competence &&
                        f.ClassificationStatus == "Classified")
            .ToListAsync(ct);

        // Agrupa por grupo/subgrupo DRE
        var lines = facts
            .GroupBy(f => new { f.DreGroup, f.DreSubgroup, f.DreOrder })
            .Select(g => new
            {
                dreGroup    = g.Key.DreGroup,
                dreSubgroup = g.Key.DreSubgroup,
                dreOrder    = g.Key.DreOrder,
                amount      = g.Sum(f => f.AmountCompetence)
            })
            .OrderBy(x => x.dreOrder)
            .ToList();

        var receita   = lines.Where(l => l.dreGroup == "RECEITA").Sum(l => l.amount);
        var despesa   = lines.Where(l => l.dreGroup == "DESPESA" || l.dreGroup == "CUSTO").Sum(l => l.amount);
        var resultado = receita - despesa;

        return Ok(new
        {
            competence,
            regime    = "competencia",
            receita,
            despesa,
            resultado,
            resultadoLabel = resultado >= 0 ? "SUPERÁVIT" : "DÉFICIT",
            lines
        });
    }

    // GET /api/dre/cash?competence=YYYY-MM
    [HttpGet("api/dre/cash")]
    public async Task<IActionResult> GetDreCash(
        [FromQuery] string competence, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(competence))
            return BadRequest(new { error = "competence é obrigatório (YYYY-MM)" });

        var facts = await _context.FinancialFacts
            .Where(f => f.Competence == competence &&
                        f.ClassificationStatus == "Classified")
            .ToListAsync(ct);

        var lines = facts
            .GroupBy(f => new { f.DreGroup, f.DreSubgroup, f.DreOrder })
            .Select(g => new
            {
                dreGroup    = g.Key.DreGroup,
                dreSubgroup = g.Key.DreSubgroup,
                dreOrder    = g.Key.DreOrder,
                amount      = g.Sum(f => f.AmountCash)
            })
            .OrderBy(x => x.dreOrder)
            .ToList();

        var receita   = lines.Where(l => l.dreGroup == "RECEITA").Sum(l => l.amount);
        var despesa   = lines.Where(l => l.dreGroup == "DESPESA" || l.dreGroup == "CUSTO").Sum(l => l.amount);
        var resultado = receita - despesa;

        return Ok(new
        {
            competence,
            regime    = "caixa",
            receita,
            despesa,
            resultado,
            resultadoLabel = resultado >= 0 ? "SUPERÁVIT" : "DÉFICIT",
            lines
        });
    }
}
