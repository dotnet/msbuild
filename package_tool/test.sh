
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

run_setup(){
	bash $DIR/setup/test_setup.sh
}

run_unit_tests(){
	bats $DIR/test/unit_tests/test_debian_build_lib.bats
	bats $DIR/test/unit_tests/test_scripts.bats
}

run_integration_tests(){
	input_dir=$DIR/test/test_assets/test_package_layout
	output_dir=$DIR/p_out

	# Create output dir
	mkdir -p $output_dir

	# Build the actual package
	sudo $DIR/package_tool $input_dir $output_dir

	# Integration Test Entrypoint placed by package_tool
	bats $output_dir/test_package.bats

	# Cleanup output dir
	rm -rf $DIR/test/test_assets/test_package_output
}

run_all(){
	#run_setup
	run_unit_tests
	run_integration_tests
}

run_all
