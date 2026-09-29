using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DeliciasDaNilda.Common;

/// <summary>
/// Lê decimais de formulário aceitando os dois formatos que chegam na prática:
/// "0.5" (o que o navegador envia em &lt;input type="number"&gt;) e "0,5" /
/// "1.234,50" (digitado em pt-BR). Sem isso, com a cultura pt-BR o ponto vira
/// separador de milhar e "0.5" era lido como 5.
/// </summary>
public class DecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var resultado = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (resultado == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, resultado);
        var texto = resultado.FirstValue?.Trim();

        if (string.IsNullOrEmpty(texto))
        {
            // Campo vazio: null para decimal?, erro de obrigatório para decimal.
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) is null)
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Informe um valor.");
            }

            return Task.CompletedTask;
        }

        if (TryParse(texto, out var valor))
        {
            bindingContext.Result = ModelBindingResult.Success(valor);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"\"{texto}\" não é um número válido.");
        }

        return Task.CompletedTask;
    }

    public static bool TryParse(string texto, out decimal valor)
    {
        var normalizado = texto.Replace(" ", string.Empty);
        var temVirgula = normalizado.Contains(',');
        var temPonto = normalizado.Contains('.');

        if (temVirgula && temPonto)
        {
            // Formato pt-BR completo: ponto de milhar, vírgula decimal.
            normalizado = normalizado.Replace(".", string.Empty).Replace(',', '.');
        }
        else if (temVirgula)
        {
            normalizado = normalizado.Replace(',', '.');
        }

        return decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }
}

public class DecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var tipo = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return tipo == typeof(decimal) ? new DecimalModelBinder() : null;
    }
}
