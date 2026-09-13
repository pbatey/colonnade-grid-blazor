# Row selection

Set `EnableRowSelection="true"` to add a checkbox column with select-all in
the header. Selection is exposed as `@bind-SelectedKeys`
(`IReadOnlySet<string>`), independent of `GridState`.

Provide `RowKey` (`Func<TItem, string>`) to control how a row's identity is
computed:

- **Required, and validated at render time, when `DataProvider` is set.**
  Without it, selection would be keyed by object identity — which silently
  breaks the moment a provider-backed reload deserializes new instances for
  the same logical rows.
- **Optional for the `Items` path**, where each object instance gets its own
  key for as long as it lives. That needs `TItem` to be a reference type: with
  a value type (a `record struct`, say), every boxed copy is a different
  object, so the grid requires `RowKey` whenever `EnableRowSelection` is set
  and throws at render time without it.

Selection persists across sort/filter/group changes. The header checkbox is
tri-state (checked/unchecked/indeterminate) reflecting only the *currently
visible* rows — "select all N across remote pages" is out of scope for v1
(see [Known limitations](known-limitations.md)).
