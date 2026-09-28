using SourceAFIS;

namespace BioService.Backends;

/// <summary>
/// Utilidad compartida para matching SourceAFIS 1:N.
/// Usado por todos los backends que necesiten comparar templates.
/// </summary>
public static class SourceAfisHelper
{
    /// <summary>Umbral mínimo de score para considerar un match positivo.</summary>
    public const double MATCH_THRESHOLD = 40.0;

    /// <summary>
    /// Busca el template más similar en un diccionario de templates almacenados.
    /// </summary>
    public static Task<IdentifyResult> IdentifyAsync(byte[] probeBytes, Dictionary<int, byte[]> storedTemplates)
    {
        return Task.Run(() =>
        {
            try
            {
                var probe = new FingerprintTemplate(probeBytes);
                var matcher = new FingerprintMatcher(probe);

                double bestScore = 0;
                int bestPin = 0;

                foreach (var (pin, templateBytes) in storedTemplates)
                {
                    var candidate = new FingerprintTemplate(templateBytes);
                    double score = matcher.Match(candidate);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPin = pin;
                    }
                }

                if (bestScore >= MATCH_THRESHOLD)
                {
                    return new IdentifyResult(true, Pin: bestPin, Score: bestScore);
                }

                return new IdentifyResult(false, Msg: "Huella no reconocida.", Score: bestScore);
            }
            catch (Exception ex)
            {
                return new IdentifyResult(false, Msg: $"Error en matching: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Serializa un FingerprintTemplate a bytes para almacenar en BD.
    /// </summary>
    public static byte[] Serialize(FingerprintTemplate template)
    {
        return template.ToByteArray();
    }

    /// <summary>
    /// Deserializa un FingerprintTemplate desde bytes almacenados en BD.
    /// </summary>
    public static FingerprintTemplate Deserialize(byte[] data)
    {
        return new FingerprintTemplate(data);
    }
}
