## v1.2.29 (patch)

Changes since v1.2.28:

- Refuse an array constant in [JsonRequiredIfSiblingIs] at rule compilation [patch] (Claude)
- Null-check the comparer property explicitly before reading it (Claude)
- Time eligibility over a ten-type clique to guard its memoization (Claude)
- Pair a string dictionary key through the dictionary's own comparer [patch] (Claude)
- Keep the caller's reader MaxDepth when re-reading a claimed subtree [patch] (Claude)
- Report every failing element of a root list, array or dictionary [patch] (matt-edmondson)
- Merge remote-tracking branch 'origin/main' into fix/63-key-parsing-without-default-reflection (Claude)
- Cover both outcomes of a failing key converter [patch] (Claude)
- Pair non-string dictionary keys without the global default resolver [patch] (Claude)

