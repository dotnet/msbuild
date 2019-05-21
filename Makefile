all-mono:
	./eng/cibuild_bootstrapped_msbuild.sh --host_type mono --configuration Release --binaryLog --skip_tests ${ARGS}

test-mono:
	./eng/cibuild_bootstrapped_msbuild.sh --host_type mono --configuration Release --binaryLog ${ARGS}

clean-%: clean

clean:
	rm -Rf artifacts .packages
