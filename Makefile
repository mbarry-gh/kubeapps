.PHONY: help create delete deploy check clean app webv test load-test

help :
	@echo "Usage:"
	@echo "   make create           - create a K3d cluster"
	@echo "   make delete           - delete the K3d cluster"
	@echo "   make deploy           - deploy the apps to the cluster"
	@echo "   make check            - check the endpoints with curl"
	@echo "   make test             - run a WebValidate test"
	@echo "   make load-test        - run a 60 second WebValidate test"
	@echo "   make clean            - delete the apps from the cluster"
	@echo "   make app              - build and deploy a local app docker image"
	@echo "   make webv             - build and deploy a local WebV docker image"
	@echo "   make jumpbox          - deploy a 'jumpbox' pod"

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

deploy :
	# deploy the app
	@# continue on most errors
	@kubectl apply -f deploy/ngsa-memory

	# deploy prometheus and grafana
	@kubectl apply -f deploy/prometheus
	@kubectl apply -f deploy/grafana

	# deploy fluent bit
	@kubectl apply -f deploy/fluentbit

	# wait for the pods to start
	@kubectl wait pod -n monitoring --for condition=ready --all --timeout=30s
	@kubectl wait pod -n logging fluentbit --for condition=ready --timeout=30s

	# deploy WebV after the app starts
	@kubectl wait pod ngsa-memory --for condition=ready --timeout=30s
	@kubectl apply -f deploy/webv
	@kubectl wait pod webv --for condition=ready --timeout=30s

	# display pod status
	@kubectl get po -A | grep "default\|monitoring\|logging"

check :
	# curl all of the endpoints
	@curl localhost:30080/version
	@echo "\n"
	@curl localhost:30088/version
	@echo "\n"
	@curl localhost:30000
	@curl localhost:32000

clean :
	# delete the deployment
	@# continue on error
	-kubectl delete pod jumpbox --ignore-not-found=true
	-kubectl delete -f deploy/webv --ignore-not-found=true
	-kubectl delete -f deploy/ngsa-memory --ignore-not-found=true
	-kubectl delete ns monitoring --ignore-not-found=true
	-kubectl delete ns logging --ignore-not-found=true

	# show running pods
	@kubectl get po -A

app :
	# build push the local image
	docker build ../ngsa-app -t k3d-registry.localhost:5000/ngsa-app:local
	docker push k3d-registry.localhost:5000/ngsa-app:local

	# delete WebV
	-kubectl delete -f deploy/webv --ignore-not-found=true

	# delete/deploy the app
	-kubectl delete -f deploy/ngsa-memory --ignore-not-found=true
	kubectl apply -f deploy/ngsa-local

	# deploy WebValidate after app starts
	@kubectl wait pod ngsa-memory --for condition=ready --timeout=30s
	kubectl apply -f deploy/webv
	@kubectl wait pod webv --for condition=ready --timeout=30s

	@kubectl get po

	# display the app version
	-http localhost:30080/version

webv :
	# build and push the local image
	docker build ../webvalidate -t k3d-registry.localhost:5000/webv:local
	docker push k3d-registry.localhost:5000/webv:local
	
	# delete / create WebValidate
	-kubectl delete -f deploy/webv --ignore-not-found=true
	kubectl apply -f deploy/webv-local
	kubectl wait pod webv --for condition=ready --timeout=30s
	@kubectl get po

	# display the current version
	-http localhost:30088/version

test :
	# use WebValidate to run a test
	cd webv && webv --verbose --summary tsv --server http://localhost:30080 --files baseline.json
	# the 400 and 404 results are expected
	# Errors and ValidationErrorCount should both be 0

load-test :
	# use WebValidate to run a 60 second test
	cd webv && webv --verbose --server http://localhost:30080 --files benchmark.json --run-loop --sleep 100 --duration 60
