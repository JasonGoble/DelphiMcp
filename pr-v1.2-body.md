## v1.2 Deprecation of Library-Specific Tools

This PR implements v1.2 preparation by adding deprecation notices to library-specific tools and guiding users to the unified tools.

### Changes

1. **RtlTools.cs & DevExpressTools.cs**:
   - Add `[Obsolete]` attributes to deprecated tool methods
   - Update method descriptions to recommend unified tools
   - Provide clear migration guidance in warnings

2. **Documentation**:
   - Create ADR 0009: Library-Specific Tool Removal in v1.2
   - Add v1.2 Breaking Changes section to README
   - Document deprecation timeline and migration path

3. **Test Status**:
   - ✅ Build: Clean (4 existing warnings in Program.cs)
   - ✅ Tests: 19/19 passing

### Deprecation Timeline

- **v1.1** (released): Unified tools available; old tools functional
- **v1.2** (now): Old tools emit `[Obsolete]` compiler warnings; profiles optional
- **v1.3** (next): Old tools removed entirely (semver major bump)

### Migration Path

Users have through v1.2 to migrate to unified tools:
- Replace `search_rtl` → `search_delphi_source(library="rtl")`
- Replace `lookup_rtl_class` → `lookup_delphi_class(library="rtl")`
- Replace `search_devexpress` → `search_delphi_source(library="devexpress")`
- Replace `lookup_devexpress_class` → `lookup_delphi_class(library="devexpress")`

See README v1.2 Breaking Changes section and ADR 0009 for details.
