# Current iteration — Configurable product catalog

## Scope

First delivery, Business backend only. Base and pull request target: `develop`.
Working branch: `feature/catalog-categories`. Logbook:
`docs/bitacora-categorias-catalogo.md`.

Persist two configurable levels, categories and subcategories. Products may
reference a subcategory. Existing products start unclassified and retain their
price, activation state and image. Classification starts empty. Administrators create the data. All product creates and edits require a description. Administrators can create and edit both levels. Authenticated users
can read the classification and filter the product catalog.

Applicable design requirements: RD-01, RD-02, RD-03, RD-06, RD-07, RD-08,
RD-09 and RD-12. Source: `docs/requirements/design-rules.md`. This catalog
delivery has no separate assignment brief or functional requirement ID in
the requirements index.

## Acceptance criteria

- [x] Existing products retain price, state and image after migration.
- [x] Classification starts empty and authenticated users can list what administrators create.
- [x] An administrator can add and rename categories and subcategories.
- [x] Product reads expose the classification and support filters by category,
      subcategory and unclassified status. Existing unfiltered reads still work.
- [x] Product writes reject unknown subcategories and blank descriptions for every product.
- [x] Standard users can read the catalog and cannot change its classification.
- [x] Rejected writes leave the stored data unchanged.
- [x] Build and tests pass. The README documents verified API examples.
- [x] ADR 021 and a separate logbook commit accompany the pull request.

## Out of scope

Invoice options, add-on pricing, tostones selections and changes to invoice
records belong to the second delivery and its own branch.
