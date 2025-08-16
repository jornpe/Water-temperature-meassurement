#!/usr/bin/env sh
set -eu

: "${API_BASE_URL?Need to set API_BASE_URL, e.g. http://backend:8080 or https://api.example.com}"

# Render nginx config from template using envsubst
export API_BASE_URL
envsubst '${API_BASE_URL}' < /etc/nginx/templates/default.conf.template > /etc/nginx/conf.d/default.conf

# Start nginx
exec nginx -g 'daemon off;'
