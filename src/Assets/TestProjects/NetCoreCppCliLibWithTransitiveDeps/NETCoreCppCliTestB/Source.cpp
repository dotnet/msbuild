#include <vector>


public ref class NETCoreCppB abstract sealed
{
public:
	static System::String^ SimpleMethodB()
	{
		System::String^ ret = NETCoreCppC::SimpleMethodC();

		std::vector<int> v;
		v.push_back(1);

		return ret;
	}
};