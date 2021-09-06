.PHONY: help create delete test load-test

help :
	@echo "Usage:"
	@echo "   make create           - create a K3d cluster"
	@echo "   make delete           - delete the K3d cluster"
	@echo "   make test             - run a WebValidate test"
	@echo "   make load-test        - run a 60 second WebValidate test"

delete :
	# delete the cluster (if exists)
	@# this will fail harmlessly if the cluster does not exist
	@k3d cluster delete

create : delete
	@# create the cluster and wait for ready
	@# this will fail harmlessly if the cluster exists
	@# default cluster name is k3d

	@k3d cluster create --registry-use k3d-registry.localhost:5000 --config deploy/k3d.yaml --k3s-server-arg "--no-deploy=traefik" --k3s-server-arg "--no-deploy=servicelb"

	# wait for cluster to be ready
	@kubectl wait node --for condition=ready --all --timeout=60s
	@sleep 5
	@kubectl apply -f deploy/flux/flux.yaml
	@kubectl wait pod -A --all --for condition=ready --timeout=30s

test :
	# use WebValidate to run a test
	cd webv && webv --verbose --summary tsv --server http://localhost:30082 --files baseline.json
	# the 400 and 404 results are expected
	# Errors and ValidationErrorCount should both be 0

load-test :
	# use WebValidate to run a 60 second test
	cd webv && webv --verbose --server http://localhost:30082 --files benchmark.json --run-loop --sleep 100 --duration 60
