<script setup lang="ts">
import { Inbox } from 'lucide-vue-next'

defineProps<{
  title: string
  description: string
}>()
</script>

<template>
  <div class="rounded-md border border-dashed bg-card p-8 text-center">
    <Inbox :size="28" class="mx-auto text-muted-foreground" />
    <h2 class="mt-4 text-base font-medium">
      {{ title }}
    </h2>
    <p class="mx-auto mt-2 max-w-md text-sm leading-6 text-muted-foreground">
      {{ description }}
    </p>
  </div>
</template>
