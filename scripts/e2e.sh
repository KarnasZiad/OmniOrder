#!/usr/bin/env bash
# End-to-end scenario through the API gateway, against the docker compose stack:
# click & collect order -> stock reserved (RabbitMQ) -> prepared -> collected -> stock decremented.
set -euo pipefail

GATEWAY=${GATEWAY:-http://localhost:8000}
SKU=${SKU:-TSH-001}

wait_for() { # url, timeout seconds
  for _ in $(seq 1 "$2"); do curl -fs "$1" > /dev/null && return 0; sleep 1; done
  echo "timeout waiting for $1" >&2; return 1
}

wait_for_status() { # order id, expected status
  for _ in $(seq 1 60); do
    status=$(curl -fs "$GATEWAY/api/ordering/orders/$1" | jq -r .status)
    [ "$status" = "$2" ] && return 0
    sleep 1
  done
  echo "order $1 is '$status', expected '$2'" >&2; return 1
}

wait_for "$GATEWAY/health/ready" 120

STORE=$(curl -fs "$GATEWAY/api/inventory/stores" | jq -r '.[0].id')
echo "store: $STORE"

curl -fs -X POST "$GATEWAY/api/inventory/stores/$STORE/stock/$SKU/receipts" \
  -H 'Content-Type: application/json' -d '{"quantity": 5}' > /dev/null
BEFORE=$(curl -fs "$GATEWAY/api/inventory/stores/$STORE/stock" | jq -r ".[] | select(.sku==\"$SKU\") | .onHand")
echo "on hand before: $BEFORE"

ORDER=$(curl -fs -X POST "$GATEWAY/api/ordering/orders" -H 'Content-Type: application/json' -d "{
  \"storeId\": \"$STORE\", \"mode\": \"ClickAndCollect\",
  \"customerName\": \"E2E Test\", \"customerEmail\": \"e2e@example.com\",
  \"lines\": [{\"sku\": \"$SKU\", \"quantity\": 2}]
}" | jq -r .id)
echo "order: $ORDER"

wait_for_status "$ORDER" Confirmed
echo "stock reserved by Inventory, order confirmed"

curl -fs -X POST "$GATEWAY/api/ordering/orders/$ORDER/ready" > /dev/null
curl -fs -X POST "$GATEWAY/api/ordering/orders/$ORDER/collect" > /dev/null
wait_for_status "$ORDER" Collected

for _ in $(seq 1 30); do
  AFTER=$(curl -fs "$GATEWAY/api/inventory/stores/$STORE/stock" | jq -r ".[] | select(.sku==\"$SKU\") | .onHand")
  if [ "$AFTER" -eq $((BEFORE - 2)) ]; then
    echo "on hand after pickup: $AFTER"
    echo "E2E OK"
    exit 0
  fi
  sleep 1
done
echo "stock was not decremented (before $BEFORE, after $AFTER)" >&2
exit 1
