---
applyTo: "src/Ruvents.UI/**"
---

# UI and rendering

These rules govern shared UI and, through the repository-wide instructions,
Razor, styling, navigation, and rendering work in the server, browser host, and
tests. Keep the repository-wide project boundaries and validation requirements in
force, and the [C# and Razor conventions](csharp.instructions.md) for code-behind.
For account behavior, also read the [account guide](account.instructions.md) and
the [server guide](server.instructions.md), including when shared UI changes can
affect static Identity pages. See the [root README](../../README.md) for the
styling and layout architecture and [build rules](../../build/README.md) for
enforcement.

## Razor components

- Every page/component must have a matching `.razor.cs` partial class, including
  markup-only components. `_Imports.razor` does not need code-behind.
- Put component members and logic in code-behind. Do not add `@code` or `@functions`
  blocks. Rendering expressions and control flow remain in `.razor` markup.
- Match the generated component's namespace, class name, and generic parameters.
  `_Imports.razor` using directives do not apply to `.razor.cs` files; add the C#
  imports the code-behind needs.
- Preserve Razor routes, rendering, authorization, injection, and component
  parameter contracts when moving code. Do not edit generated C# files.
- Mark caller-supplied `[Parameter]` properties with `[EditorRequired]` when the
  component needs them to serve its purpose. Keep optional inputs optional and
  assess route, query, form, and cascading inputs separately. Use ordinary
  auto-properties, not C# `required` or `init`. This is a Razor call-site check,
  not runtime validation or a guarantee of non-null values.
- Treat `[Parameter]` properties as caller-owned inputs. Keep editable local state
  separate and use `EventCallback` to request parent updates; reconcile incoming
  parameter changes deliberately. `MA0121` is build-enforced for parameter
  overwrites, including when the parameter is `[EditorRequired]`. Keep Blazor
  `[CascadingParameter]` inputs on the same terms.
- Components that own asynchronous work cancel superseded operations and discard
  stale results instead of overwriting newer state. Component lifetime is the
  cancellation owner for interactive work; the repository-wide
  [cancellation rules](../copilot-instructions.md#validation) own the shared token
  requirements, including the ban on serializing tokens or reusing a completed
  prerender request's token in a circuit.

## Layout and styling

- This solution has no UI component library. The visual identity lives in
  `src/Ruvents.UI/wwwroot/theme.css` as design tokens plus shared element and card
  styling, and each page may add a scoped `.razor.css` for local layout. Extend
  those tokens and component classes instead of inventing new colours or one-off
  styling, and keep meaning in text as well as colour. Do not add a component
  framework, a CSS framework, a CSS preprocessor, or utility classes.
- `Ruvents.UI` is a Razor class library, so its `wwwroot` files are served under
  `_content/Ruvents.UI/`, not the site root: reference them as
  `@Assets["_content/Ruvents.UI/theme.css"]`. A root path such as `/theme.css`
  returns 404.
- Use native elements (`<button>`, `<a>`, `<ul>`, `<section>`) and Blazor's own
  components (`NavLink`, `AuthorizeView`, `PageTitle`, `AntiforgeryToken`,
  `EditForm` with the `Input*` family, `ValidationMessage`, `Router`,
  `AuthorizeRouteView`, `FocusOnNavigate`). Do not create a component whose only
  job is forwarding attributes to a native element; `<button type="button">` with
  `@onclick` is the stock click handler.
- `src/Ruvents/wwwroot/app.css` holds functional rules only — keeping form controls
  at 16px so mobile browsers do not zoom on focus, and bounding `.table-container`
  so wide tables scroll instead of widening the page. Presentational rules belong in
  `theme.css` (shared design system) or a scoped `.razor.css` (local layout), never
  in `app.css`.
- Layout lives in a few CSS Grids: `MainLayout.razor.css` (named `header`,
  `navigation`, `content`, and `footer` areas, one column below 48rem),
  `ManageLayout.razor.css` (account menu beside the content once there is room),
  and `Home.razor.css` (an `auto-fit` two-section grid). Add structure to these
  grids rather than introducing a layout framework.
- The navigation is always rendered and stacks above the content on narrow
  screens. Do not reintroduce a hidden drawer, toggle widget, or overlay; they
  need JavaScript and focus management that static SSR does not provide.
- On narrow screens the navigation tiles wrap into even columns; never give the
  rail `overflow-x`. A single scrolling row hides destinations with no affordance,
  and only the page may scroll horizontally. `NavigationTests` asserts that no
  navigation item sits outside the viewport.
- A malformed `.razor.css` rule is not a build error. It reaches the generated
  scoped-CSS bundle as written, and an unterminated block swallows every following
  rule in the assembly, breaking unrelated pages. Verify scoped-CSS edits with a
  browser test.
- Do not reintroduce Bootstrap, `!important`, or ornamental animations, and do not
  hand-write inline styles for ordinary layout or spacing.
- Contain user-supplied text instead of letting it widen the layout. Keep
  `min-width: 0` on grid cells and add `overflow-wrap: anywhere` where unbroken
  strings can appear; registration uses the email address as the user name, so the
  navigation can hold one long token. Never rely on `min-width: 0` alone to wrap
  text, and clamp grid track minimums with `min(100%, <size>)` so enlarged browser
  text cannot force horizontal scrolling.
- Keep enhanced navigation enabled for shell links. Identity POSTs still perform
  normal navigation to refresh cookies and authentication state. Keep the shell on
  document scrolling so enhanced navigation can reset and restore scroll position;
  a separate layout scrollbar needs its own handling.
- Give each navigation region its own `<nav aria-label>` containing a `<ul>` of
  `NavLink`s. Native anchors are focusable without a `tabindex` override, and
  `NavLink` supplies `aria-current="page"`. The shell provides the grid cell, not
  a second navigation landmark.
- Interactive third-party components are allowed only where the browser must do the
  work — currently Cropper.Blazor for the avatar crop. Keep their result in an
  ordinary form field so a static post still works, and keep a `<noscript>` fallback.
  Cropper.js builds its DOM itself, so its styling cannot live in a scoped
  `.razor.css`; put it in `theme.css` scoped under the component's own class
  (`.photo-crop`). The vendor stylesheet loads after `theme.css`, so an unscoped,
  equal-specificity rule silently loses to it.
- Colour mode is explicit state: `theme.js` toggles `data-theme` on the root element
  and `theme.css` owns both dark blocks. Add new presentational state to that
  attribute rather than to inline styles.
- `NavLink` renders the anchor itself, so a scoped `.razor.css` rule never matches it
  (the scope attribute stops at the component). Style navigation in `theme.css`.
- Keep native Identity inputs and submit buttons: their static SSR form mapping,
  browser autofill, and passkey submitter contracts must work without an
  interactive form. Do not replace them with interactive components.
- Make layouts shrink and wrap on narrow screens, preserve visible keyboard
  focus, and put form labels before their controls. Preserve semantic links,
  buttons, tables, and all Blazor/Identity form and reconnect hooks.
- Shared notices use `class="notice"` with `data-kind="success"`, `"error"`,
  `"warning"`, or `"information"`. Keep meaning in the message, not just color.
  bUnit tests assert this contract.
- Opt plain GET forms into enhanced handling with `data-enhance` when they target
  a Blazor page. Keep Identity, logout, and other cookie-changing POSTs as normal
  navigations.

## Progressive web app

- `service-worker.js` and the asset manifest are generated at build time from
  hashed inputs. Do not hand-edit generated files, and do not add caching
  strategies outside `build/PwaAssets.targets` and `build/PwaWorker.js`.
- Registration lives in `src/Ruvents.UI/wwwroot/pwa.js`. Offer an update only
  with user consent, and reload only the tab that requested the update; never
  refresh every client to apply a new version.

## Rendering and first paint
- An interactively rendered page must not replace prerendered content with an
  empty loading state when its renderer starts. Use a structural skeleton when a
  read is really pending rather than a single loading line, and keep
  display-only first-paint state out of server-owned data you would then have to
  re-query.
- Do not ship large prerendered state. Prerendered state travels inside the
  circuit-start message, which has a 32 KiB SignalR receive limit; values are
  data-protected, Base64-encoded, serialized, and Base64-encoded again, so raw
  JSON is a poor proxy for transport size. Measure the transport form with the
  persistence serializer's options, including null-valued properties, keep a
  budget with headroom, and fall back to the skeleton when it does not fit.
- Mirror a resolved client theme onto the root element when browser chrome
  depends on it; a body-only theme leaves the document scrollbar in the other
  scheme.

## Asynchronous component lifetime

- Components owning asynchronous I/O derive from `CancellableComponentBase` and
  link each operation through `CreateOperation()`; cancel superseded reads, keep
  stale-result checks, and dispose each source when its operation finishes.
  Override `DisposeCoreAsync` for cleanup instead of hiding disposal. The base
  type reads the `RequestAborted` cascade that the static `App` supplies, so
  server-rendered work stops with its request while interactive work is owned by
  the component lifetime. The repository-wide
  [cancellation rules](../copilot-instructions.md#validation) own the shared token
  requirements, including the ban on serializing tokens or reusing a completed
  prerender request's token in a circuit.
