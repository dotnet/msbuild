all-mono:
	./build.sh -hostType mono -configuration Release -skipTests

test-mono:
	./build.sh -hostType mono -configuration Release

all-coreclr:
	./build.sh -hostType core -skipTests

test-coreclr:
	./build.sh -hostType core

clean-%: clean

clean:
	rm -Rf artifacts
	rm -Rf build/obj
