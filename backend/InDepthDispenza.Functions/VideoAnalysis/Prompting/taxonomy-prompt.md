# Taxonomy for Tag Extraction
Use the following taxonomy to extract structured tags from the transcript.

**Your taxonomy of existing tags:**
```json
{taxonomy}
```

## Tag Extraction Rules
1. **Use existing tags**: Always prefer existing taxonomy tags
2. **Maintain hierarchy**: Tags must respect the parent-child relationships shown above in the taxonomy.

**CRITICAL RULES:**
1. **ALWAYS use existing tags from the taxonomy above when applicable**
2. **ALWAYS Include parent categories**: When using `cervical_cancer`, also include `cancer` and `healing` in the achievement.
3. **ALWAYS Include a subcategorie**: When there's cancer `cancer` there should be a subcategory!
4. **OPTIONALLY Include attributes**: such as `metastatic` in case of `cancer`.

## Proposal Rules (for the "proposals" array)
**Only propose new tags when:**
1. Content clearly doesn't match ANY existing tag
2. The tag would be reusable across multiple testimonials
3. **ONLY propose new tags if:**
   - The content describes a specific condition/practice NOT in the taxonomy
   - The new tag would be used by multiple testimonials (not one-off mentions)
4. **ALWAYS** Keep subcategories reserved for named medical diagnoses or distinct disease entities. Use attributes 
  for symptoms, functional impairments, or descriptive features (e.g. brain_fog, cognitive_fog, fatigue). If the 
  testimonial names a specific diagnosis not yet in subcategories (e.g. Alzheimer's), propose adding it.
5. **NEVER propose tags that already exist in the taxonomy**
6. **Check the entire taxonomy tree before proposing - if "cervical_cancer" exists anywhere, don't propose it**
7. You are allowed to make proposals on the complete hierarchy. So you can propose a whole new or partial hierarchy.

**CRITICAL RULES:**
1. **ALWAYS add your proposals in the related achievement.**

**Never propose:**
- Tags that already exist (search ALL categories first)
- One-off mentions that won't generalize

**proposals array should often be empty** - that means your taxonomy is working well!