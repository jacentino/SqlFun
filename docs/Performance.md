## Performance

SqlFun provides some mechanisms allowing to write high-performance data access layer:
* you have full control over your queries; you can easily analyze and tune them
* all plumbing code is generated before first use; after that not reflection is used
* provides extensions, that allow to write more query analyzer-friendly SQL
  * [inlined collection parameters](Non-standard-parameter-conversions#inlining-parameter-values)
  * [composite queries](Composite-queries)
* some provider-specific extensions are implemented, that enable extremely fast bulk operations:
  * [MS SQL TVP parameters](Non-standard-parameter-conversions#tvp-parameters)
  * [Oracle array parameters](Oracle-array-parameters)
  * [PostgreSQL array parameters](PostgreSQL-array-parameters)
  * [PostgreSQL bulk copy](PostgreSQL-Bulk-Copy)
