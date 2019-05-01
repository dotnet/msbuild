all-mono:
	./eng/cibuild_bootstrapped_msbuild.sh --host_type mono --configuration Release --binaryLog --skip_tests ${MSBUILD_PROPERTIES}

test-mono:
	./eng/cibuild_bootstrapped_msbuild.sh --host_type mono --configuration Release --binaryLog ${MSBUILD_PROPERTIES}

clean-%: clean

clean:
	rm -Rf artifacts .packages
