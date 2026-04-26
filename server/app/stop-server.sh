#!/bin/bash

set -e

echo "=========================================="
echo "   PARANDO SERVIDORES CLOVER"
echo "=========================================="

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

if [ -f "pids.txt" ]; then
    while read -r pid; do
        if kill -0 "$pid" 2>/dev/null; then
            log_info "Parando PID $pid"
            kill "$pid" 2>/dev/null || true
            sleep 1
            if kill -0 "$pid" 2>/dev/null; then
                kill -9 "$pid" 2>/dev/null || true
            fi
        fi
    done < pids.txt
    rm -f pids.txt
else
    log_warning "pids.txt nao encontrado."
fi

for port in 2000 7001 7002 8080 9001 9002; do
    pids=$(lsof -ti tcp:"$port" 2>/dev/null || true)
    if [ -n "$pids" ]; then
        log_info "Limpando processos remanescentes na porta $port"
        kill $pids 2>/dev/null || true
    fi
done

log_success "Servidores Clover parados."
