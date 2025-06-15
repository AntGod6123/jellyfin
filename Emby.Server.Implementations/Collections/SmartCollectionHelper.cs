using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Emby.Server.Implementations.Collections
{
    internal static class SmartCollectionHelper
    {
        public static IEnumerable<BaseItem> ExecuteQuery(string query, User? user, ILibraryManager libraryManager)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Enumerable.Empty<BaseItem>();
            }

            var libs = libraryManager.RootFolder.Children
                .Where(f => libraryManager.GetLibraryOptions(f).IncludeInGlobalCollections)
                .Cast<BaseItem>()
                .ToList();

            IEnumerable<BaseItem> Search(string term)
            {
                term = term.Trim();
                if (term.Length == 0)
                {
                    return Enumerable.Empty<BaseItem>();
                }

                var q = new InternalItemsQuery(user)
                {
                    SearchTerm = term,
                    Recursive = true
                };

                return libraryManager.GetItemList(q, libs);
            }

            var orSegments = Regex.Split(query, "\\s+OR\\s+", RegexOptions.IgnoreCase);
            HashSet<BaseItem> result = new();

            foreach (var seg in orSegments)
            {
                var tokens = Regex.Split(seg.Trim(), "\\s+");
                HashSet<BaseItem>? segSet = null;
                bool notNext = false;
                foreach (var token in tokens)
                {
                    if (string.Equals(token, "AND", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.Equals(token, "NOT", StringComparison.OrdinalIgnoreCase))
                    {
                        notNext = true;
                        continue;
                    }

                    var items = Search(token);
                    if (segSet is null)
                    {
                        segSet = new HashSet<BaseItem>(items);
                        continue;
                    }

                    if (notNext)
                    {
                        segSet.ExceptWith(items);
                        notNext = false;
                    }
                    else
                    {
                        segSet.IntersectWith(items);
                    }
                }

                if (segSet is not null)
                {
                    result.UnionWith(segSet);
                }
            }

            return result;
        }
    }
}
