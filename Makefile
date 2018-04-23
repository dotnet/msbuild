all-mono:
	./build.sh -host mono -configuration Release -skipTests

test-mono:
	./build.sh -host mono -configuration Release

all-coreclr:
	./build.sh -host core -skipTests

test-coreclr:
	./build.sh -host core

clean-%: clean

clean:
	rm -Rf artifacts
