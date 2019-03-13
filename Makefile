all-mono:
	./eng/cibuild_bootstrapped_msbuild.sh --host_type mono --configuration Release --binaryLog --skip_tests

test-mono:
	./eng/cibuild_bootstrapped_msbuild.sh --host_type mono --configuration Release --binaryLog

clean-%: clean

clean:
	rm -Rf artifacts .packages
