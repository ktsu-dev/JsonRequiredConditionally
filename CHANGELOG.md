## v1.2.0 (minor)

Changes since v1.1.0:

- docs: drop em-dash from the qualified presence rule [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: address final review findings for JsonRequiredAndNotEmpty [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: fix review findings in JsonRequiredAndNotEmpty docs [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: document JsonRequiredAndNotEmpty [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- test: cover nesting, naming, self-sufficiency and dedup for JsonRequiredAndNotEmpty ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: enforce JsonRequiredAndNotEmpty during the graph walk ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add EmptyProperties to JsonRequiredConditionallyException ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: claim types decorated with JsonRequiredAndNotEmptyAttribute ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: compile NonEmptyRule from JsonRequiredAndNotEmptyAttribute ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add JsonRequiredAndNotEmptyAttribute ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add EmptinessInspector for payload-element emptiness ([@matt-edmondson](https://github.com/matt-edmondson))
- chore: store icon.png in LFS as .gitattributes declares ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: scope build badge to the default branch ([@matt-edmondson](https://github.com/matt-edmondson))
- Restore the missing package icon and suppress cross-TFM APICompat noise ([@matt-edmondson](https://github.com/matt-edmondson))
- Stop Update SDKs failing when there is nothing to update ([@matt-edmondson](https://github.com/matt-edmondson))
- Fix ktsu.Sdk 2.27 analyzer errors: Polyfill PrivateAssets, netstandard framework package refs [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Update ktsu.Sdk to 2.21.1 ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove obsolete step for installing runtimes in the .NET workflow ([@matt-edmondson](https://github.com/matt-edmondson))
- Update MSTest.Sdk version and adjust ktsu.Sdk versions in global.json ([@matt-edmondson](https://github.com/matt-edmondson))
- Update copyright years and adjust output directory in configuration files ([@matt-edmondson](https://github.com/matt-edmondson))
- Add .mailmap file for author name normalization ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.9 (patch)

Changes since v1.1.8:

- Bump the ktsu group with 9 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.1.8 (patch)

Changes since v1.1.7:

- Bump the ktsu group with 9 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.1.7 (patch)

Changes since v1.1.6:

- chore: store icon.png in LFS as .gitattributes declares ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: scope build badge to the default branch ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.6 (patch)

Changes since v1.1.5:

- Restore the missing package icon and suppress cross-TFM APICompat noise ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.5 (patch)

Changes since v1.1.4:

- Stop Update SDKs failing when there is nothing to update ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.4 (patch)

Changes since v1.1.3:

- Fix ktsu.Sdk 2.27 analyzer errors: Polyfill PrivateAssets, netstandard framework package refs [patch] ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.3 (patch)

Changes since v1.1.2:

- [patch] Update ktsu.Sdk to 2.21.1 ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.2 (patch)

Changes since v1.1.1:

- Remove obsolete step for installing runtimes in the .NET workflow ([@matt-edmondson](https://github.com/matt-edmondson))
- Update MSTest.Sdk version and adjust ktsu.Sdk versions in global.json ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.1 (patch)

Changes since v1.1.0:

- Update copyright years and adjust output directory in configuration files ([@matt-edmondson](https://github.com/matt-edmondson))
- Add .mailmap file for author name normalization ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.0 (major)

- [patch] Close the Populate containment gap and narrow the write-path throw ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Contain unmodellable serializer features and fix the graph-walk defects ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Fix README enum-converter gap and CLAUDE.md test filter examples ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Add README and repo documentation ([@matt-edmondson](https://github.com/matt-edmondson))
- Use ktsu.KtsuBuild.Tool for release verification in Task 9 ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Pin record, aggregation, and absent-sibling semantics ([@matt-edmondson](https://github.com/matt-edmondson))
- Rewrite Task 9 README content for the graph-validation design ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Cover naming policy and case sensitivity ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Match constructor-bound properties against System.Text.Json's own selected constructor ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Recognize constructor-bound properties; unify rule compilation on JsonTypeInfo ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Replace reflection member model with JsonTypeInfo; fix eligibility factorial cost ([@matt-edmondson](https://github.com/matt-edmondson))
- Fix JsonStringEnumConverter omission in Task 7/8 test code and correct TFM count ([@matt-edmondson](https://github.com/matt-edmondson))
- Update design spec for JsonTypeInfo member model and dropped net5.0/net6.0 targets ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Fix review findings: indexer crash, IDictionary keying, JsonIgnore/hiding, violation paths ([@matt-edmondson](https://github.com/matt-edmondson))
- Update design spec for whole-graph validation and MissingProperties paths ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Validate whole object graphs instead of relying on converter re-entrancy ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Add converter, factory, and nested graph validation ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Add JSON property presence scanning ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Resolve AmbiguousMatchException when a hidden sibling member is looked up ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Add requirement rule compilation ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Add JsonRequiredConditionallyException ([@matt-edmondson](https://github.com/matt-edmondson))
- [fix] Use [SuppressMessage] attribute instead of pragma directives for CA1008 ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Add enum-normalizing value comparison ([@matt-edmondson](https://github.com/matt-edmondson))
- [minor] Add JsonRequiredIfSiblingIs attribute and repo scaffolding ([@matt-edmondson](https://github.com/matt-edmondson))
- Merge nesting task into the converter task in the plan ([@matt-edmondson](https://github.com/matt-edmondson))
- Add implementation plan for ktsu.JsonRequiredConditionally ([@matt-edmondson](https://github.com/matt-edmondson))
- Pin sibling-resolution and absent-sibling semantics in design spec ([@matt-edmondson](https://github.com/matt-edmondson))
- Add design spec for ktsu.JsonRequiredConditionally ([@matt-edmondson](https://github.com/matt-edmondson))

