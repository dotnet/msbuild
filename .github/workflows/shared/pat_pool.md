---
description: Agentic workflow import to integrate the Copilot PAT Pool

jobs:
  pat_pool:
    environment: ${{ github.aw.import-inputs.environment }}
    needs: [pre_activation]
    runs-on: ubuntu-slim
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
          COPILOT_PAT_8: ${{ github.aw.import-inputs.COPILOT_PAT_8 }}
          COPILOT_PAT_9: ${{ github.aw.import-inputs.COPILOT_PAT_9 }}
          RANDOM_SEED:   ${{ github.aw.import-inputs.random_seed   }}
        shell: bash
        run: |
          # Collect pool entries with non-empty secrets from COPILOT_PAT_0..COPILOT_PAT_9.
          PAT_NUMBERS=()
          POOL_INDICATORS=(➖ ➖ ➖ ➖ ➖ ➖ ➖ ➖ ➖ ➖)

          for i in $(seq 0 9); do
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
            warning_message+="(checked COPILOT_PAT_0 through COPILOT_PAT_9)"
            echo "$warning_message"
            exit 0
          fi

          # Drop PATs that GitHub rejects as bad credentials (expired or revoked)
          # so they are never selected. Any other response (including network
          # errors) keeps the PAT, and if every PAT is rejected the full pool is
          # kept so the failure surfaces in the agent job as before.
          VALID_PAT_NUMBERS=()
          for i in "${PAT_NUMBERS[@]}"; do
            var="COPILOT_PAT_${i}"
            status=$(curl -s -o /dev/null -w '%{http_code}' --max-time 15 \
              -H "Authorization: Bearer ${!var}" \
              -H "Accept: application/vnd.github+json" \
              https://api.github.com/user || true)
            if [ "$status" = "401" ]; then
              echo "::warning::COPILOT_PAT_${i} was rejected (HTTP 401) and is excluded from selection"
              POOL_INDICATORS[${i}]="❌"
            else
              VALID_PAT_NUMBERS+=(${i})
            fi
          done

          if [ ${#VALID_PAT_NUMBERS[@]} -gt 0 ]; then
            PAT_NUMBERS=("${VALID_PAT_NUMBERS[@]}")
          fi

          # Select a random index using the seed if specified
          if [ -n "$RANDOM_SEED" ]; then
            RANDOM=$RANDOM_SEED
          fi

          PAT_INDEX=$(( RANDOM % ${#PAT_NUMBERS[@]} ))
          PAT_NUMBER="${PAT_NUMBERS[$PAT_INDEX]}"
          POOL_INDICATORS[${PAT_NUMBER}]="✅"

          echo "Pool size: ${#PAT_NUMBERS[@]}"
          echo "Selected PAT number ${PAT_NUMBER} (index: ${PAT_INDEX})"

          # Emit a markdown table of the pool entries to the step summary
          echo "|0|1|2|3|4|5|6|7|8|9|" >> "$GITHUB_STEP_SUMMARY"
          echo "|-|-|-|-|-|-|-|-|-|-|" >> "$GITHUB_STEP_SUMMARY"
          (IFS='|'; printf '|%s' "${POOL_INDICATORS[@]}"; printf '|\n') >> "$GITHUB_STEP_SUMMARY"

          # Set the PAT number as the output
          echo "copilot_pat_number=${PAT_NUMBER}" >> "$GITHUB_OUTPUT"

import-schema:
  environment:
    type: string
    required: true
  COPILOT_PAT_0:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_0 }}
  COPILOT_PAT_1:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_1 }}
  COPILOT_PAT_2:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_2 }}
  COPILOT_PAT_3:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_3 }}
  COPILOT_PAT_4:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_4 }}
  COPILOT_PAT_5:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_5 }}
  COPILOT_PAT_6:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_6 }}
  COPILOT_PAT_7:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_7 }}
  COPILOT_PAT_8:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_8 }}
  COPILOT_PAT_9:
    type: string
    required: false
    default: ${{ secrets.COPILOT_PAT_9 }}
  random_seed:
    type: string
    required: false
    description: >-
      A seed number to use for the random PAT number selection,
      for deterministic selection if needed.
---
