# Sharpscope Metrics — Detailed Guide 📚

This page defines **all 44 metrics**, including formulas, notes, interpretation tips, and edge-case handling as implemented by Sharpscope.

> Legend:  
> - `m` = number of methods in a type  
> - `n` = number of fields in a type  
> - `μ(a)` = number of methods that access field `a`

---

## 1) Summary (15)

### 1.1 `TotalNamespaces`
- **What:** total number of namespaces in the analyzed codebase.

### 1.2 `TotalTypes`
- **What:** total number of declared types (class/struct/interface/enum/record).

### 1.3 `MeanTypesPerNamespace`
- **Formula:** `TotalTypes / TotalNamespaces` (0 if no namespaces).

### 1.4–1.7 SLOC Stats
- **`TotalSloc`**: total **Source Lines of Code** across all types.  
- **`AvgSlocPerType`**: mean SLOC per type.  
- **`MedianSlocPerType`**: median SLOC per type.  
- **`StdDevSlocPerType`**: population std. dev. of SLOC per type.

### 1.8–1.11 Methods Stats
- **`TotalMethods`**: total number of methods.  
- **`AvgMethodsPerType`**, **`MedianMethodsPerType`**, **`StdDevMethodsPerType`**: stats over method counts per type.

### 1.12–1.15 Complexity Stats
- **`TotalComplexity`**: sum of **CYCLO** across all methods (equivalently, sum of **WMC** per type).  
- **`AvgComplexityPerType`**, **`MedianComplexityPerType`**, **`StdDevComplexityPerType`**: stats over per-type complexity (WMC).

---

## 2) Namespaces (2)

### 2.1 `NOC` — Number of Classes/Types
- **What:** number of types inside the namespace.

### 2.2 `NAC` — Number of Abstract Classes
- **What:** number of abstract classes (and abstract records).  
- **Note:** interfaces are **not** counted for NAC.

---

## 3) Types (10)

### 3.1 `SLOC` — Source Lines of Code (per type)
- **What:** sum of method SLOC plus any type-level code measured by the adapter (for C#, comes from Roslyn IR).

### 3.2 `NOM` — Number of Methods
- **What:** total declared methods in the type.

### 3.3 `NPM` — Number of Public Methods
- **What:** subset of methods where visibility is public.

### 3.4 `WMC` — Weighted Methods per Class
- **Formula:** `WMC = Σ CYCLO(method)` over the type’s methods.

### 3.5 `DEP` — Dependencies
- **What:** **distinct** referenced types (internal + external) by this type.  
- **Source:** type-level reference list in the IR (adapter-resolved).

### 3.6 `I-DEP` — Internal Dependencies
- **What:** **distinct** internal referenced types by this type.  
- **Source:** internal type dependency graph.

### 3.7 `FAN-IN`
- **What:** number of **other types** that depend on this type (internal in-degree).

### 3.8 `FAN-OUT`
- **What:** number of **other types** referenced by this type (internal out-degree).

### 3.9 `NOA` — Number of Attributes/Fields
- **What:** count of fields in the type.

### 3.10 `LCOM3` — Lack of Cohesion in Methods
- **Formula (bounded):**  
  `LCOM3 = 1 - (Σ μ(a) / (m * n))`, bounded to `[0, 1]`.  
  **Degenerate cases:** if `m ≤ 1` or `n ≤ 1` ⇒ `LCOM3 = 0.0`.  
- **Intuition:** lower is better (more cohesive). 0 means every field is used by every method.

---

## 4) Methods (5)

### 4.1 `MLOC` — Method Lines of Code
- **What:** SLOC per method (adapter-derived).

### 4.2 `CYCLO` — Cyclomatic Complexity
- **Formula:** `1 + decisionPoints`  
- **Decision points (C#):** `if/else-if`, `switch` arms, loops, `catch`, boolean operators `&&`/`||`, pattern matches, ternary and null-coalescing conditions (adapter-dependent nuances).

### 4.3 `CALLS` — Number of Invocations
- **What:** count of call sites (static/instance/extension).

### 4.4 `NBD` — Nested Block Depth
- **What:** maximum nesting depth (blocks/scopes) in the method body.

### 4.5 `PARAM` — Number of Parameters
- **What:** parameter count, excluding implicit receivers.

---

## 5) Namespace Coupling (5)

### 5.1 `CA` — Afferent Coupling
- **What:** number of **other namespaces** that depend on this one (incoming edges).

### 5.2 `CE` — Efferent Coupling
- **What:** number of **other namespaces** this one depends on (outgoing edges).

### 5.3 `I` — Instability
- **Formula:** `I = CE / (CA + CE)` (0 if `CA + CE = 0`).  
- **Range:** `[0, 1]`, higher means more unstable (more outgoing than incoming).

### 5.4 `A` — Abstractness
- **Formula:** `A = (#abstract types + interfaces) / (#types)` (namespace scope).

### 5.5 `D` — Normalized Distance from Main Sequence
- **Formula:** `D = |A + I − 1|`.  
- **Interpretation:** closer to 0 is better (balance between abstractness and stability).

---

## 6) Type Coupling (4)

### 6.1 `DEP`
- **What:** distinct dependencies (internal + external) of the type.

### 6.2 `I-DEP`
- **What:** distinct internal dependencies of the type.

### 6.3 `FAN-IN`
- **What:** internal in-degree (#types depending on this one).

### 6.4 `FAN-OUT`
- **What:** internal out-degree (#types referenced by this one).

---

## 7) Dependencies (3) — Solution-level

### 7.1 `DEP`
- **What:** total distinct dependency edges **declared by types** (includes external targets).

### 7.2 `I-DEP`
- **What:** total distinct **internal** dependency edges (from the internal graph).

### 7.3 `Cycles`
- **What:** strongly connected components (SCCs) with more than 1 node (cycles).  
- **Scopes:** computed for both **Type** and **Namespace** graphs (Tarjan’s algorithm under the hood).

---

## Notes & Edge Cases

- **Empty sets:** statistics return `0.0` for empty inputs; instability is `0.0` when `CA + CE = 0`.  
- **Self-loops:** ignored in coupling counts (they don’t inform cross-entity coupling).  
- **LCOM3 bounding:** final value clamped to `[0,1]`; degenerate cases return `0.0`.  
- **Adapter nuances:** exact SLOC/CYCLO/CALLS/NBD depend on the language adapter (Roslyn for C#).
