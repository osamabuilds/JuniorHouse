A role-aware home, **Today**, so staff land on their work and not on Reference data (the app today redirects every sign-in to `/reference-data`).

- **Banner.** One line only when something needs a decision (here stock alerts), with a single action.
- **Counts.** Four stat tiles that are links: to confirm by phone, to pick, to pack, stock alerts. A tile shows only if the signed-in role has the permission; counts come from the existing queues (orders call-due, pick queue, pack queue, stock alerts).
- **Work queue.** The same items as a list, each with a title, a clarifying line and a count, ordered by urgency. Only items the role can open appear.
- **System and activity.** Outbox health (today in the footer) and the latest audit events.

All figures are placeholders. The numbers and the queue order are proposals; the counts need no new data beyond the existing list endpoints, but the home page itself is not in any spec yet.
