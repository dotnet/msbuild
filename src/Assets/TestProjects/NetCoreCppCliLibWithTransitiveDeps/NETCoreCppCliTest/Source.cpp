#include <vector>

public ref class NETCoreCpp abstract sealed
{
public:
	static System::String^ SimpleMethod()
	{
		System::String^ ret = NETCoreCppB::SimpleMethodB();

		std::vector<int> v;
		v.push_back(1);

		return ret;
	}
};