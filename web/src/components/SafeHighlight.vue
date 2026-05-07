<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  text: string
}>()

interface HighlightSegment {
  text: string
  highlighted: boolean
}

const segments = computed(() => parseHighlight(props.text))

function parseHighlight(text: string): HighlightSegment[] {
  const result: HighlightSegment[] = []
  let remaining = text

  while (remaining) {
    const start = remaining.indexOf('<mark>')
    if (start < 0) {
      result.push({ text: remaining, highlighted: false })
      break
    }

    if (start > 0) {
      result.push({ text: remaining.slice(0, start), highlighted: false })
    }

    const afterStart = remaining.slice(start + '<mark>'.length)
    const end = afterStart.indexOf('</mark>')
    if (end < 0) {
      result.push({ text: remaining.slice(start), highlighted: false })
      break
    }

    result.push({ text: afterStart.slice(0, end), highlighted: true })
    remaining = afterStart.slice(end + '</mark>'.length)
  }

  return result
}
</script>

<template>
  <span>
    <template v-for="(segment, index) in segments" :key="index">
      <mark v-if="segment.highlighted" class="rounded-sm bg-primary/15 px-1 text-primary">{{ segment.text }}</mark>
      <span v-else>{{ segment.text }}</span>
    </template>
  </span>
</template>
