#!/usr/bin/env bash
set -euo pipefail

root="$(pwd)"

# --- 0) Garante Directory.Build.props válido (se existir vazio, sobrescreve com XML mínimo) ---
cat > "$root/Directory.Build.props" <<'XML'
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
XML

# --- Helpers ---

# Pasca-case por trecho (mantém restante do trecho como está)
pascalize_segment() {
  local s="${1}"
  if [[ -z "$s" ]]; then echo ""; return; fi
  printf "%s%s" "${s:0:1}" | tr '[:lower:]' '[:upper:]'
  printf "%s" "${s:1}"
}

# Converte um nome de projeto tipo "sharpscope.domain" -> "Sharpscope.Domain"
project_to_ns() {
  local proj="$1"
  local IFS='.'
  read -ra parts <<< "$proj"
  local ns=""
  for p in "${parts[@]}"; do
    [[ -z "$p" ]] && continue
    if [[ -n "$ns" ]]; then ns+="."; fi
    ns+="$(pascalize_segment "$p")"
  done
  printf "%s" "$ns"
}

# Converte um caminho "Models/Deep/Nested" -> ".Models.Deep.Nested" (com PascalCase)
path_to_ns_suffix() {
  local rel="$1"
  # remove filename se veio junto
  rel="${rel%/*}"
  [[ "$rel" == "$1" ]] || true
  local IFS='/'
  read -ra parts <<< "$rel"
  local suf=""
  for p in "${parts[@]}"; do
    [[ -z "$p" ]] && continue
    if [[ -n "$suf" ]]; then suf+="."; fi
    suf+="$(pascalize_segment "$p")"
  done
  [[ -n "$suf" ]] && printf ".%s" "$suf" || printf ""
}

# Determina namespace com base em:
# - src/<Layer>/<Project>/...  ou  tests/<Project>/...
resolve_namespace_for_file() {
  local f="$1"          # caminho relativo a partir da raiz
  local proj=""
  local rest=""
  if [[ "$f" == src/* ]]; then
    # src/<Layer>/<Project>/...
    local tmp="${f#src/}"             # remove 'src/'
    tmp="${tmp#*/}"                   # remove '<Layer>/'
    proj="${tmp%%/*}"                 # pega '<Project>'
    rest="${tmp#"$proj"/}"            # pega o restante depois de '<Project>/'
  elif [[ "$f" == tests/* ]]; then
    # tests/<Project>/...
    local tmp="${f#tests/}"
    proj="${tmp%%/*}"
    rest="${tmp#"$proj"/}"
  else
    # fallback: usa pasta onde está o .csproj (aproximação)
    proj="${f%%/*}"
    rest="${f#"$proj"/}"
  fi

  local base_ns
  base_ns="$(project_to_ns "$proj")"
  local suf
  suf="$(path_to_ns_suffix "$rest")"
  printf "%s%s" "$base_ns" "$suf"
}

# Gera conteúdo para Program.cs da API (Minimal API)
write_api_program() {
  local f="$1"
  cat > "$f" <<'CS'
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Sharpscope API is running.");

app.Run();
CS
}

# Gera conteúdo para Program.cs do Terminal (top-level)
write_cli_program() {
  local f="$1"
  cat > "$f" <<'CS'
Console.WriteLine("Sharpscope CLI");
CS
}

# Gera stub de interface/class/static class/teste xUnit conforme heurística
write_generic_stub() {
  local f="$1"
  local ns
  ns="$(resolve_namespace_for_file "$f")"

  local file="$(basename "$f")"
  local name="${file%.cs}"

  local is_contract=false
  local is_extensions=false
  local is_test=false

  [[ "$f" == */Contracts/* ]] && is_contract=true
  [[ "$name" == *Extensions ]] && is_extensions=true
  [[ "$f" == tests/* && "$name" == *Tests ]] && is_test=true

  if $is_test; then
    cat > "$f" <<CS
using Xunit;

namespace $ns;

public class $name
{
    [Fact]
    public void Placeholder() { }
}
CS
  elif $is_contract; then
    cat > "$f" <<CS
namespace $ns;

public interface $name
{
}
CS
  elif $is_extensions; then
    cat > "$f" <<CS
namespace $ns;

public static class $name
{
}
CS
  else
    cat > "$f" <<CS
namespace $ns;

public class $name
{
}
CS
  fi
}

# --- 1) Preenche Program.cs especiais (API e CLI) ---

api_prog="src/Presentation/sharpscope.api/Program.cs"
cli_prog="src/Presentation/sharpscope.terminal/Program.cs"

if [[ -f "$api_prog" ]]; then
  write_api_program "$api_prog"
else
  echo "⚠️  Não encontrei $api_prog (ok, seguindo)."
fi

if [[ -f "$cli_prog" ]]; then
  write_cli_program "$cli_prog"
else
  echo "⚠️  Não encontrei $cli_prog (ok, seguindo)."
fi

# --- 2) Varre todos os .cs e cria stubs vazios (exceto os Program.cs já tratados) ---

while IFS= read -r -d '' file; do
  # pula Program.cs da API/CLI (já escritos)
  if [[ "$file" == "$api_prog" || "$file" == "$cli_prog" ]]; then
    continue
  fi
  # só processa se já existe
  if [[ -f "$file" ]]; then
    write_generic_stub "$file"
  fi
done < <(find src tests -type f -name "*.cs" -print0)

echo "✅ Stubs escritos. Tente um 'dotnet build'."
