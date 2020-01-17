#include <vector>

public ref class NETCoreCppC abstract sealed
{
public:
	static System::String^ SimpleMethodC()
	{
		System::String^ ret = "Hello, World!";

		std::vector<int> v;
		v.push_back(1);

		return ret;
	}
};