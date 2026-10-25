Search results and the empty state. The header field and the search overlay both go to `/search?q=`; this is the page they land on.

- **Results.** Breadcrumb, the search field (so the query can be changed in place, a plain GET form that works without scripting), `display-lg` "Results for “dress”", the `FilterBar` (All / Girls / Boys / Age / Size and Sort, "Best match" first), the product grid with sale badges where a product is discounted, and **Show more** (keyset paging; the page does not jump).
- **No results.** `display-lg` "No results for “frock”", one helpful sentence, popular searches as chips (from the settings), the top-level links, and a "New in" row so it is never a dead end.

Rules: the query is rendered as text, never as markup (SC-9); results pages are `noindex` (search is not for crawlers) and filtered or sorted variants carry a canonical (SEO-06); an empty state is a real 200 page, not a soft 404; focus moves to the results heading after a new search. Placeholders: product names and prices.
