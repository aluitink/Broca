namespace Broca.ActivityPub.Core.Models;

public class CollectionSearchParameters
{
    public string? Filter { get; set; }
    public string? Search { get; set; }
    public string? OrderBy { get; set; }

    public bool HasSearchCriteria =>
        !string.IsNullOrWhiteSpace(Filter) ||
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(OrderBy);

    public string ToQueryString()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(Filter))
            parts.Add($"$filter={Uri.EscapeDataString(Filter)}");

        if (!string.IsNullOrWhiteSpace(Search))
            parts.Add($"$search={Uri.EscapeDataString(Search)}");

        if (!string.IsNullOrWhiteSpace(OrderBy))
            parts.Add($"$orderby={Uri.EscapeDataString(OrderBy)}");

        return string.Join("&", parts);
    }

    public static CollectionSearchParameters? FromQueryString(string? queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
            return null;

        var query = queryString.TrimStart('?');
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var parameters = new CollectionSearchParameters();

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2) continue;

            var key = Uri.UnescapeDataString(parts[0]);
            var value = Uri.UnescapeDataString(parts[1]);

            switch (key)
            {
                case "$filter":
                    parameters.Filter = value;
                    break;
                case "$search":
                    parameters.Search = value;
                    break;
                case "$orderby":
                    parameters.OrderBy = value;
                    break;
            }
        }

        return parameters.HasSearchCriteria ? parameters : null;
    }
}
