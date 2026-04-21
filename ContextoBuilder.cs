using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;

public static class ContextoBuilder
{
    private static bool YnToBool(string? v) =>
        string.Equals(v?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);

    public static List<ContextoDto> BuildFromQuery(SqlConnection conn, string sql)
    {
        // Diccionario de contextos por ID
        var ctxMap = new Dictionary<string, ContextoDto>(StringComparer.OrdinalIgnoreCase);

        // Por cada contexto, guardo los grupos en un map interno para evitar duplicados
        var grpMapByCtx = new Dictionary<string, Dictionary<string, GrupoDto>>(StringComparer.OrdinalIgnoreCase);

        using var cmd = new SqlCommand(sql, conn);
        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            string idContexto = rdr["ID_Contexto"]?.ToString()?.Trim() ?? "";
            string idGrupo = rdr["ID_Grupo"]?.ToString()?.Trim() ?? "";
            string idCaract = rdr["ID_Caracteristica"]?.ToString()?.Trim() ?? "";
            bool selMultiple = YnToBool(rdr["SelMultiple"]?.ToString());
            bool obligatorio = YnToBool(rdr["Obligatorio"]?.ToString());

            if (string.IsNullOrWhiteSpace(idContexto) || string.IsNullOrWhiteSpace(idGrupo) || string.IsNullOrWhiteSpace(idCaract))
                continue;

            // Contexto
            if (!ctxMap.TryGetValue(idContexto, out var ctx))
            {
                ctx = new ContextoDto
                {
                    Id = idContexto,
                    Nombre = idContexto
                };
                ctxMap[idContexto] = ctx;
                grpMapByCtx[idContexto] = new Dictionary<string, GrupoDto>(StringComparer.OrdinalIgnoreCase);
            }

            // Grupo dentro del contexto
            var grpMap = grpMapByCtx[idContexto];
            if (!grpMap.TryGetValue(idGrupo, out var grp))
            {
                grp = new GrupoDto
                {
                    IdGrupo = idGrupo,
                    NombreGrupo = idGrupo,
                    Obligatorio = obligatorio,
                    SelMultiple = selMultiple
                };
                grpMap[idGrupo] = grp;
                ctx.Grupos.Add(grp);
            }
            else
            {
                // Si por alguna razón viniera mezclado, “consolidás” con OR para no perder true
                grp.Obligatorio = grp.Obligatorio || obligatorio;
                grp.SelMultiple = grp.SelMultiple || selMultiple;
            }

            // Detalle (evitar duplicados)
            if (!grp.Detalle.Any(d => string.Equals(d.IdCaract, idCaract, StringComparison.OrdinalIgnoreCase)))
            {
                grp.Detalle.Add(new DetalleDto
                {
                    IdCaract = idCaract,
                    NombreCaract = idCaract
                });
            }
        }

        // Ordenar para que el JSON sea “estable” y prolijo
        foreach (var c in ctxMap.Values)
        {
            c.Grupos = c.Grupos
                .OrderBy(g => g.IdGrupo, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var g in c.Grupos)
            {
                g.Detalle = g.Detalle
                    .OrderBy(d => d.IdCaract, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        return ctxMap.Values
            .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
