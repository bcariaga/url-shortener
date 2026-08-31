#!/usr/bin/env bash
set -euo pipefail

project_dir="/home/deploy/url-shortener/deploy"
compose_file="${project_dir}/compose.yml"
env_file="${project_dir}/.env.runtime"
caddy_fragment="${project_dir}/Caddyfile.fragment"
caddy_file="/opt/infra/proxy/compose/Caddyfile"
caddy_container="bunko-caddy"
project_name="short"
public_network="short_public"
image="${1:?Usage: deploy-production.sh <api-image> <image-archive>}"
image_archive="${2:?Usage: deploy-production.sh <api-image> <image-archive>}"

cd "${project_dir}"

for required_file in "${compose_file}" "${env_file}" "${caddy_fragment}" "${image_archive}"; do
  if [[ ! -f "${required_file}" ]]; then
    echo "Required deploy file is missing: ${required_file}"
    exit 1
  fi
done

for required_key in POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD URL_SHORTENER_TOKEN URL_SHORTENER_OWNER_ID; do
  if ! grep -qE "^${required_key}=.+" "${env_file}"; then
    echo "Required runtime key is missing: ${required_key}"
    exit 1
  fi
done

if ! grep -qx 'URL_SHORTENER_OWNER_ID=braian' "${env_file}"; then
  echo "URL_SHORTENER_OWNER_ID must be braian"
  exit 1
fi

if ! [[ "${image}" =~ ^ghcr\.io/[a-z0-9._/-]+:[A-Fa-f0-9]{40}$ ]]; then
  echo "The API image must be a GHCR image tagged with a full commit SHA"
  exit 1
fi

if grep -q '^API_IMAGE=' "${env_file}"; then
  sed -i "s|^API_IMAGE=.*|API_IMAGE=${image}|" "${env_file}"
else
  printf '\nAPI_IMAGE=%s\n' "${image}" >> "${env_file}"
fi

gunzip -c "${image_archive}" | docker load >/dev/null
docker image inspect "${image}" >/dev/null
rm -f "${image_archive}"

if ! docker network inspect "${public_network}" >/dev/null 2>&1; then
  docker network create "${public_network}" >/dev/null
fi

if ! docker inspect -f "{{if index .NetworkSettings.Networks \"${public_network}\"}}yes{{end}}" "${caddy_container}" | grep -qx yes; then
  docker network connect "${public_network}" "${caddy_container}"
fi

docker compose --project-name "${project_name}" --env-file "${env_file}" -f "${compose_file}" config --quiet
docker compose --project-name "${project_name}" --env-file "${env_file}" -f "${compose_file}" pull postgres redis aspire-dashboard
docker compose --project-name "${project_name}" --env-file "${env_file}" -f "${compose_file}" up -d --wait postgres redis
docker compose --project-name "${project_name}" --env-file "${env_file}" -f "${compose_file}" run --rm migrate
docker compose --project-name "${project_name}" --env-file "${env_file}" -f "${compose_file}" up -d --remove-orphans api aspire-dashboard

caddy_dir="$(dirname "${caddy_file}")"
caddy_candidate="$(mktemp "${caddy_dir}/.Caddyfile.short.candidate.XXXXXX")"
caddy_backup="$(mktemp "${caddy_dir}/.Caddyfile.short.backup.XXXXXX")"
cleanup_caddy_files() {
  rm -f "${caddy_candidate}" "${caddy_backup}"
}
trap cleanup_caddy_files EXIT

start_marker="# BEGIN url-shortener managed"
end_marker="# END url-shortener managed"

awk -v start="${start_marker}" -v end="${end_marker}" '
  $0 == start { skipping = 1; next }
  $0 == end { skipping = 0; next }
  !skipping { print }
  END { if (skipping) exit 42 }
' "${caddy_file}" > "${caddy_candidate}"

printf '\n%s\n' "${start_marker}" >> "${caddy_candidate}"
sed -n '1,$p' "${caddy_fragment}" >> "${caddy_candidate}"
printf '%s\n' "${end_marker}" >> "${caddy_candidate}"

cp "${caddy_file}" "${caddy_backup}"
cp "${caddy_candidate}" "${caddy_file}"
if ! docker exec "${caddy_container}" caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile; then
  cp "${caddy_backup}" "${caddy_file}"
  echo "Caddy validation failed; the previous configuration was restored"
  exit 1
fi
docker exec "${caddy_container}" caddy reload --address localhost:2018 --config /etc/caddy/Caddyfile --adapter caddyfile

compose() {
  docker compose --project-name "${project_name}" --env-file "${env_file}" -f "${compose_file}" "$@"
}

assert_network() {
  local service="$1"
  local network="$2"
  local container_id
  container_id="$(compose ps -q "${service}")"
  if [[ -z "${container_id}" ]] || ! docker inspect -f "{{if index .NetworkSettings.Networks \"${network}\"}}yes{{end}}" "${container_id}" | grep -qx yes; then
    echo "Service ${service} is not attached to ${network}"
    exit 1
  fi
}

assert_not_network() {
  local service="$1"
  local network="$2"
  local container_id
  container_id="$(compose ps -q "${service}")"
  if docker inspect -f "{{if index .NetworkSettings.Networks \"${network}\"}}yes{{end}}" "${container_id}" | grep -qx yes; then
    echo "Service ${service} must not be attached to ${network}"
    exit 1
  fi
}

assert_no_host_ports() {
  local service="$1"
  local container_id
  local port_bindings
  container_id="$(compose ps -q "${service}")"
  port_bindings="$(docker inspect -f '{{json .HostConfig.PortBindings}}' "${container_id}")"
  if [[ "${port_bindings}" != "null" && "${port_bindings}" != "{}" ]]; then
    echo "Service ${service} unexpectedly publishes a host port"
    exit 1
  fi
}

for service in postgres redis; do
  assert_network "${service}" short_internal
  assert_not_network "${service}" "${public_network}"
  assert_no_host_ports "${service}"
done

for service in api aspire-dashboard; do
  assert_network "${service}" short_internal
  assert_network "${service}" "${public_network}"
  assert_no_host_ports "${service}"
done

if ! docker inspect -f "{{if index .NetworkSettings.Networks \"${public_network}\"}}yes{{end}}" "${caddy_container}" | grep -qx yes; then
  echo "Caddy is not attached to ${public_network}"
  exit 1
fi
compose ps
