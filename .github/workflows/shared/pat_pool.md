---
description: Agentic workflow import to integrate the Copilot PAT Pool

jobs:
  pat_pool:
    runs-on: ubuntu-slim
    # This job only selects a pool index; it never needs repository scopes.
    # Set no permissions explicitly so it cannot inherit elevated defaults
    # from a consuming workflow.
    permissions: {}
    outputs:
      pat_number: ${{ steps.select-pat-number.outputs.copilot_pat_number }}
    steps:
      - id: select-pat-number
        name: Select Copilot token from pool
        env:
          COPILOT_PAT_0: ${{ github.aw.import-inputs.COPILOT_PAT_0 }}
          COPILOT_PAT_1: ${{ github.aw.import-inputs.COPILOT_PAT_1 }}
          COPILOT_PAT_2: ${{ github.aw.import-inputs.COPILOT_PAT_2 }}
          COPILOT_PAT_3: ${{ github.aw.import-inputs.COPILOT_PAT_3 }}
          COPILOT_PAT_4: ${{ github.aw.import-inputs.COPILOT_PAT_4 }}
          COPILOT_PAT_5: ${{ github.aw.import-inputs.COPILOT_PAT_5 }}
          COPILOT_PAT_6: ${{ github.aw.import-inputs.COPILOT_PAT_6 }}
          COPILOT_PAT_7: ${{ github.aw.import-inputs.COPILOT_PAT_7 }}
          RANDOM_SEED: ${{ github.aw.import-inputs.random_seed }}
        shell: bash
        run: |
          # Collect pool entries with non-empty secrets from COPILOT_PAT_0..COPILOT_PAT_7.
          PAT_NUMBERS=()
          POOL_INDICATORS=(➖ ➖ ➖ ➖ ➖ ➖ ➖ ➖)

          for i in $(seq 0 7); do
            var="COPILOT_PAT_${i}"
            val="${!var}"
            if [ -n "$val" ]; then
              PAT_NUMBERS+=(${i})
              POOL_INDICATORS[${i}]="🟪"
            fi
          done

          # If none of the entries in the pool have values, emit a warning
          # and do not set an output value. The consumer can fall back to
          # using COPILOT_GITHUB_TOKEN.
          if [ ${#PAT_NUMBERS[@]} -eq 0 ]; then
            warning_message="::warning::None of the PAT pool entries had values "
            warning_message+="(checked COPILOT_PAT_0 through COPILOT_PAT_7)"
            echo "$warning_message"
            exit 0
          fi

          # Select a random index using the seed if specified. Only honor an
          # integer seed; ignore any other value so a malformed seed (e.g. a
          # non-integer) can never abort the job under `set -e`.
          case "$RANDOM_SEED" in
            '' | *[!0-9]*) ;; # empty or non-integer: leave RANDOM unseeded
            *) RANDOM=$RANDOM_SEED ;;
          esac

          PAT_INDEX=$(( RANDOM % ${#PAT_NUMBERS[@]} ))
          PAT_NUMBER="${PAT_NUMBERS[$PAT_INDEX]}"
          POOL_INDICATORS[${PAT_NUMBER}]="✅"

          echo "Pool size: ${#PAT_NUMBERS[@]}"
          echo "Selected PAT number ${PAT_NUMBER} (index: ${PAT_INDEX})"

          # Emit a markdown table of the pool entries to the step summary
          echo "|0|1|2|3|4|5|6|7|" >> "$GITHUB_STEP_SUMMARY"
          echo "|-|-|-|-|-|-|-|-|" >> "$GITHUB_STEP_SUMMARY"
          (IFS='|'; printf '|%s' "${POOL_INDICATORS[@]}"; printf '|\n') >> "$GITHUB_STEP_SUMMARY"

          # Set the PAT number as the output
          echo "copilot_pat_number=${PAT_NUMBER}" >> "$GITHUB_OUTPUT"

import-schema:
  COPILOT_PAT_0:
    type: string
    required: false
    default: ${{ secrets.COPILOT_GITHUB_TOKEN }}
  COPILOT_PAT_1:
    type: string
    required: false
    default: ${{ secrets.COPILOT_GITHUB_TOKEN_2 }}
  COPILOT_PAT_2:
    type: string
    required: false
    default: ${{ secrets.COPILOT_GITHUB_TOKEN_3 }}
  COPILOT_PAT_3:
    type: string
    required: false
    default: ${{ secrets.COPILOT_GITHUB_TOKEN_4 }}
  COPILOT_PAT_4:
    type: string
    required: false
    default: ${{ secrets.COPILOT_GITHUB_TOKEN_5 }}
  COPILOT_PAT_5:
    type: string
    required: false
    default: ${{ secrets.COPILOT_GITHUB_TOKEN_6 }}
  COPILOT_PAT_6:
    type: string
    required: false
    default: ${{ secrets.COPILOT_GITHUB_TOKEN_7 }}
  COPILOT_PAT_7:
    type: string
    required: false
    default: ${{ secrets.COPILOT_GITHUB_TOKEN_8 }}
  random_seed:
    type: string
    required: false
    description: >-
      An integer seed for the random PAT number selection, for deterministic
      selection if needed. Non-integer values are ignored.
---
