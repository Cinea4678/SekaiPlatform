<script setup lang="ts">
import { Construction } from 'lucide-vue-next'
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'

const route = useRoute()
const title = computed(() => route.meta.title || '功能准备中')
const description = computed(() => route.meta.description || '该能力正在准备中。')
</script>

<template>
  <Card>
    <CardHeader>
      <Construction :size="24" class="text-primary" />
      <CardTitle>{{ title }}</CardTitle>
    </CardHeader>
    <CardContent>
      <p class="text-sm leading-6 text-muted-foreground">
        {{ description }}
      </p>
      <p class="mt-4 text-xs text-muted-foreground">
        当前登录和租户权限已生效，完整操作能力开放后会自动沿用当前身份。
      </p>
    </CardContent>
  </Card>
</template>
