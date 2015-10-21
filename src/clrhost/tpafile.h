#ifndef TPAFILE_H
#define TPAFILE_H

#include <vector>

#include "pal.h"

struct tpaentry_t
{
    pal::string_t asset_type;
    pal::string_t library_name;
    pal::string_t library_version;
    pal::string_t library_hash;
    pal::string_t relative_path;
    pal::string_t absolute_path;
    pal::string_t asset_name;
};

class tpafile
{
public:
    static std::pair<bool, tpafile> load(pal::string_t path);

    inline const std::vector<tpaentry_t>& entries() { return m_entries; }
    inline bool present() { return m_present; }

    void add_from(const pal::string_t& dir);
    void write_tpa_list(pal::string_t& output);
    void write_native_paths(std::string& output);
    void set_package_paths(std::vector<pal::string_t> search_paths) { m_package_search_paths = search_paths; }

private:
    tpafile(bool present, std::vector<tpaentry_t> entries) : m_present(present), m_entries(entries) {}

    bool m_present;
    std::vector<tpaentry_t> m_entries;
    std::vector<pal::string_t> m_package_search_paths;
};

#endif // TPAFILE_H
